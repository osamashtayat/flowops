import { onlineManager, type QueryClient } from '@tanstack/react-query'
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
  createConnection?: () => HubLike | Promise<HubLike>
  /** Delays before the 1st, 2nd… attempt to (re)start; the last one repeats. */
  retryDelaysMs?: readonly number[]
  /** Whether the server can be reached (the connectivity monitor drives TanStack's `onlineManager`). */
  online?: OnlineSource
}

export interface OnlineSource {
  isOnline(): boolean
  subscribe(listener: (online: boolean) => void): () => void
}

const DEFAULT_RETRY_DELAYS_MS = [2_000, 5_000, 15_000, 30_000, 60_000] as const
/** SignalR's own quick retries after a dropped connection; after these it gives up and we restart ourselves. */
const AUTOMATIC_RECONNECT_MS = [0, 2_000, 10_000, 30_000]

/** SignalR is loaded when realtime starts, in its own chunk: the first screen doesn't need it (issue 101). */
async function createHubConnection(): Promise<HubLike> {
  const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr')
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
 * delay. While the server can't be reached at all it stops trying, and tries again as soon as the
 * connection is back. Returns a function that stops it.
 */
export function startRealtime(queryClient: QueryClient, options: RealtimeOptions = {}): () => void {
  const createConnection = options.createConnection ?? createHubConnection
  const delays = options.retryDelaysMs ?? DEFAULT_RETRY_DELAYS_MS
  const online = options.online ?? onlineManager
  let stopped = false
  let waitingForNetwork = false
  let retryTimer: ReturnType<typeof setTimeout> | undefined
  let hasBeenUp = false
  let failedAttempts = 0
  let connection: HubLike | undefined

  async function createWiredConnection(): Promise<HubLike> {
    const hub = await createConnection()
    for (const event of REALTIME_EVENTS) {
      hub.on(event, (payload) => invalidateForEvent(queryClient, event, payload))
    }
    hub.onreconnected(() => invalidateAllRealtime(queryClient))
    // Automatic reconnect has given up (or the server closed us): start over, slowly.
    hub.onclose(() => {
      if (!stopped) scheduleRetry()
    })
    return hub
  }

  function scheduleRetry() {
    if (!online.isOnline()) {
      waitingForNetwork = true
      return
    }
    const delay = delays[Math.min(Math.max(failedAttempts - 1, 0), delays.length - 1)] ?? 0
    retryTimer = setTimeout(() => void connect(), delay)
  }

  async function connect() {
    let hub: HubLike
    try {
      // Creating can fail too (the SignalR chunk didn't download); it is retried like a failed start.
      hub = connection ??= await createWiredConnection()
      if (stopped) return
      await hub.start()
    } catch {
      if (stopped) return
      failedAttempts += 1
      scheduleRetry()
      return
    }
    if (stopped) return void hub.stop()
    // Anything that happened while we were not connected was missed.
    if (hasBeenUp || failedAttempts > 0) invalidateAllRealtime(queryClient)
    hasBeenUp = true
    failedAttempts = 0
  }

  const stopListening = online.subscribe((isOnline) => {
    if (!isOnline || !waitingForNetwork || stopped) return
    waitingForNetwork = false
    clearTimeout(retryTimer)
    void connect()
  })

  void connect()
  return () => {
    stopped = true
    stopListening()
    clearTimeout(retryTimer)
    void connection?.stop().catch(() => undefined)
  }
}
