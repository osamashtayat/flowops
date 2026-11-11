import { onlineManager, type QueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { ApiError, NETWORK_ERROR_CODE } from '@/lib/api/apiError'

/**
 * - `online`: the server answers.
 * - `offline`: the browser says it has no network.
 * - `serverUnreachable`: the browser has a network, but requests don't reach the server (no
 *   internet behind the Wi-Fi, VPN dropped, API or proxy down).
 */
export type ConnectionStatus = 'online' | 'offline' | 'serverUnreachable'

export interface ConnectivitySnapshot {
  status: ConnectionStatus
  /** A health check is in flight. */
  checking: boolean
}

export interface ConnectivityMonitor {
  subscribe(listener: () => void): () => void
  getSnapshot(): ConnectivitySnapshot
  /** Checks the server now (the banner's "Try again"). */
  checkNow(): void
  /** Starts listening (browser events, query results, TanStack's online state); returns the stop function. Can start again after a stop. */
  start(): () => void
}

export interface ConnectivityOptions {
  /** Resolves true when the server answered. Defaults to `GET /api/health`. */
  probe?: () => Promise<boolean>
  /** Delays between health checks while the server is unreachable; the last one repeats. */
  retryDelaysMs?: readonly number[]
  /** Where `online`/`offline` events come from (the window). */
  events?: EventTarget
  /** The browser's own guess (`navigator.onLine`). */
  isBrowserOnline?: () => boolean
}

const DEFAULT_RETRY_DELAYS_MS = [2_000, 5_000, 15_000, 30_000] as const
/** Gateway answers from a proxy whose upstream (the API) isn't there. */
const GATEWAY_FAILURE_STATUSES = new Set([502, 503, 504])

/** True when a failed call says nothing about the request itself, only that the server wasn't reached. */
export function isConnectionFailure(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false
  return error.code === NETWORK_ERROR_CODE || GATEWAY_FAILURE_STATUSES.has(error.status)
}

/** TanStack's default: follow the browser's online/offline events. Restored when the monitor stops. */
function browserOnlineListener(setOnline: (online: boolean) => void) {
  const online = () => setOnline(true)
  const offline = () => setOnline(false)
  window.addEventListener('online', online)
  window.addEventListener('offline', offline)
  return () => {
    window.removeEventListener('online', online)
    window.removeEventListener('offline', offline)
  }
}

async function probeHealth(): Promise<boolean> {
  try {
    await api.GET('/api/health')
    return true
  } catch {
    return false
  }
}

/**
 * The app's one notion of "can we reach the server" (docs/UI_SPEC.md §2). It drives TanStack
 * Query's `onlineManager`, so while not online every query pauses (no stream of failing requests,
 * cached data stays on screen) and refetches when the connection is back. Signals: the browser's
 * online/offline events, connection failures of any query or mutation, and any success.
 * While the server is unreachable it checks `GET /api/health` with a growing delay.
 */
export function createConnectivityMonitor(queryClient: QueryClient, options: ConnectivityOptions = {}): ConnectivityMonitor {
  const probe = options.probe ?? probeHealth
  const delays = options.retryDelaysMs ?? DEFAULT_RETRY_DELAYS_MS
  const events = options.events ?? window
  const isBrowserOnline = options.isBrowserOnline ?? (() => navigator.onLine)
  const listeners = new Set<() => void>()
  let snapshot: ConnectivitySnapshot = { status: isBrowserOnline() ? 'online' : 'offline', checking: false }
  let failedChecks = 0
  let retryTimer: ReturnType<typeof setTimeout> | undefined
  let stopped = true
  let setQueryOnline: (online: boolean) => void = () => undefined

  function update(next: Partial<ConnectivitySnapshot>) {
    const merged = { ...snapshot, ...next }
    if (merged.status === snapshot.status && merged.checking === snapshot.checking) return
    snapshot = merged
    setQueryOnline(snapshot.status === 'online')
    for (const listener of listeners) listener()
  }

  function markOnline() {
    clearTimeout(retryTimer)
    failedChecks = 0
    update({ status: 'online' })
  }

  function markLost() {
    if (snapshot.status !== 'online') return
    const status = isBrowserOnline() ? 'serverUnreachable' : 'offline'
    update({ status })
    if (status === 'serverUnreachable') scheduleCheck()
  }

  function scheduleCheck() {
    clearTimeout(retryTimer)
    const delay = delays[Math.min(failedChecks, delays.length - 1)] ?? 0
    retryTimer = setTimeout(() => void check(), delay)
  }

  async function check() {
    if (stopped || snapshot.checking || snapshot.status === 'online') return
    clearTimeout(retryTimer)
    update({ checking: true })
    const reached = await probe()
    if (stopped) return
    update({ checking: false })
    if (reached) return markOnline()
    failedChecks += 1
    const status = isBrowserOnline() ? 'serverUnreachable' : 'offline'
    update({ status })
    // With no network at all, wait for the browser's `online` event instead of polling.
    if (status === 'serverUnreachable') scheduleCheck()
  }

  function onResult(action: { type: string; error?: unknown }) {
    if (action.type === 'success') {
      if (snapshot.status !== 'online') markOnline()
    } else if ((action.type === 'failed' || action.type === 'error') && isConnectionFailure(action.error)) {
      markLost()
    }
  }

  const onBrowserOffline = () => {
    clearTimeout(retryTimer)
    update({ status: 'offline' })
  }
  const onBrowserOnline = () => void check()

  function start() {
    stopped = false
    // Take over TanStack's own online/offline listeners: the browser's guess alone misses most outages.
    onlineManager.setEventListener((setOnline) => {
      setQueryOnline = setOnline
      setOnline(snapshot.status === 'online')
      return () => {
        setQueryOnline = () => undefined
      }
    })
    events.addEventListener('offline', onBrowserOffline)
    events.addEventListener('online', onBrowserOnline)
    const stopQueries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === 'updated') onResult(event.action)
    })
    const stopMutations = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === 'updated') onResult(event.action)
    })
    if (snapshot.status === 'serverUnreachable') scheduleCheck()

    return () => {
      stopped = true
      clearTimeout(retryTimer)
      snapshot = { ...snapshot, checking: false }
      events.removeEventListener('offline', onBrowserOffline)
      events.removeEventListener('online', onBrowserOnline)
      stopQueries()
      stopMutations()
      // Back to TanStack's defaults: browser events, assumed online until one says otherwise.
      onlineManager.setEventListener(browserOnlineListener)
      onlineManager.setOnline(true)
    }
  }

  return {
    subscribe(listener) {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    getSnapshot: () => snapshot,
    checkNow: () => void check(),
    start,
  }
}
