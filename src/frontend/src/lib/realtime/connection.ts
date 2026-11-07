import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import type { QueryClient } from '@tanstack/react-query'
import { invalidateAllRealtime, invalidateForEvent, NOTIFICATIONS_HUB_PATH, REALTIME_EVENTS } from './invalidation'

/** The slice of SignalR's `HubConnection` this module uses (so tests can supply a fake). */
export interface HubLike {
  on(event: string, handler: (payload: unknown) => void): void
  onreconnected(handler: () => void): void
  onclose(handler: () => void): void
  start(): Promise<void>
  stop(): Promise<void>
}

export interface RealtimeOptions {
  createConnection?: () => HubLike
  /** Delays before the 1st, 2nd… attempt to (re)start; the last one repeats. */
  retryDelaysMs?: readonly number[]
}

const DEFAULT_RETRY_DELAYS_MS = [2_000, 5_000, 15_000, 30_000, 60_000] as const
/** SignalR's own quick retries after a dropped connection; after these it gives up and we restart ourselves. */
const AUTOMATIC_RECONNECT_MS = [0, 2_000, 10_000, 30_000]

function createHubConnection(): HubLike {
  return (
    new HubConnectionBuilder()
      // Same origin, cookie auth (ADR-0003); the Vite proxy forwards /hubs with websockets.
      .withUrl(NOTIFICATIONS_HUB_PATH)
      .withAutomaticReconnect(AUTOMATIC_RECONNECT_MS)
      // The default loggers would print payloads and URLs; there is nothing in them we need.
      .configureLogging(LogLevel.None)
      .build()
  )
}

/**
 * Connects to the notifications hub and turns server events into query invalidations
 * (API_CONTRACT §9). Failing to connect is never an error for the user: the app works without
 * realtime (pages still refetch on focus), so a failed start is retried quietly with a growing
 * delay. Returns a function that stops it.
 */
export function startRealtime(queryClient: QueryClient, options: RealtimeOptions = {}): () => void {
  const connection = (options.createConnection ?? createHubConnection)()
  const delays = options.retryDelaysMs ?? DEFAULT_RETRY_DELAYS_MS
  let stopped = false
  let retryTimer: ReturnType<typeof setTimeout> | undefined
  let hasBeenUp = false
  let failedAttempts = 0

  for (const event of REALTIME_EVENTS) {
    connection.on(event, (payload) => invalidateForEvent(queryClient, event, payload))
  }
  connection.onreconnected(() => invalidateAllRealtime(queryClient))
  // Automatic reconnect has given up (or the server closed us): start over, slowly.
  connection.onclose(() => {
    if (!stopped) scheduleRetry()
  })

  function scheduleRetry() {
    const delay = delays[Math.min(Math.max(failedAttempts - 1, 0), delays.length - 1)] ?? 0
    retryTimer = setTimeout(() => void connect(), delay)
  }

  async function connect() {
    try {
      await connection.start()
    } catch {
      if (stopped) return
      failedAttempts += 1
      scheduleRetry()
      return
    }
    if (stopped) return void connection.stop()
    // Anything that happened while we were not connected was missed.
    if (hasBeenUp || failedAttempts > 0) invalidateAllRealtime(queryClient)
    hasBeenUp = true
    failedAttempts = 0
  }

  void connect()
  return () => {
    stopped = true
    clearTimeout(retryTimer)
    void connection.stop().catch(() => undefined)
  }
}
