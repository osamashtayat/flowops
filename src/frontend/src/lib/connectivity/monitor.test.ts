import { onlineManager, QueryClient } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, NETWORK_ERROR_CODE } from '@/lib/api/apiError'
import { createConnectivityMonitor, isConnectionFailure, type ConnectivityOptions } from './monitor'

const networkError = () => new ApiError({ status: 0, code: NETWORK_ERROR_CODE })

function setup(overrides: Partial<ConnectivityOptions> = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { networkMode: 'always' } } })
  const events = new EventTarget()
  let browserOnline = true
  const probe = vi.fn<() => Promise<boolean>>().mockResolvedValue(false)
  const monitor = createConnectivityMonitor(queryClient, {
    probe,
    events,
    retryDelaysMs: [1000, 5000],
    isBrowserOnline: () => browserOnline,
    ...overrides,
  })
  const stop = monitor.start()
  const failQuery = (error: unknown) =>
    queryClient.fetchQuery({ queryKey: ['x', Math.random()], queryFn: () => Promise.reject(error) }).catch(() => undefined)
  const setBrowserOnline = (online: boolean) => {
    browserOnline = online
    events.dispatchEvent(new Event(online ? 'online' : 'offline'))
  }
  return { queryClient, monitor, stop, probe, failQuery, setBrowserOnline, status: () => monitor.getSnapshot().status }
}

describe('isConnectionFailure', () => {
  it('is true for a request that never reached the server and for gateway failures', () => {
    expect(isConnectionFailure(networkError())).toBe(true)
    for (const status of [502, 503, 504]) expect(isConnectionFailure(new ApiError({ status, code: 'unknown' }))).toBe(true)
  })

  it('is false for answers from the API itself', () => {
    for (const status of [400, 401, 403, 404, 409, 500]) expect(isConnectionFailure(new ApiError({ status, code: 'x' }))).toBe(false)
    expect(isConnectionFailure(new Error('boom'))).toBe(false)
  })
})

describe('connectivity monitor', () => {
  let stop: (() => void) | undefined
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => {
    stop?.()
    vi.useRealTimers()
  })

  it('a connection failure marks the server unreachable and pauses queries', async () => {
    const t = setup()
    stop = t.stop
    expect(t.status()).toBe('online')
    await t.failQuery(networkError())
    expect(t.status()).toBe('serverUnreachable')
    expect(onlineManager.isOnline()).toBe(false)
  })

  it('ignores errors the API answered (e.g. 500, 409)', async () => {
    const t = setup()
    stop = t.stop
    await t.failQuery(new ApiError({ status: 500, code: 'server.error' }))
    await t.failQuery(new ApiError({ status: 409, code: 'concurrency.conflict' }))
    expect(t.status()).toBe('online')
  })

  it('checks the server with growing delays and goes back online when it answers', async () => {
    const t = setup()
    stop = t.stop
    await t.failQuery(networkError())
    await vi.advanceTimersByTimeAsync(999)
    expect(t.probe).toHaveBeenCalledTimes(0)
    await vi.advanceTimersByTimeAsync(1)
    expect(t.probe).toHaveBeenCalledTimes(1)
    t.probe.mockResolvedValue(true)
    await vi.advanceTimersByTimeAsync(4999)
    expect(t.probe).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(t.status()).toBe('online')
    expect(onlineManager.isOnline()).toBe(true)
    await vi.advanceTimersByTimeAsync(60_000)
    expect(t.probe).toHaveBeenCalledTimes(2) // stops checking once online
  })

  it('checkNow checks at once and reports progress', async () => {
    const t = setup()
    stop = t.stop
    await t.failQuery(networkError())
    let answer: (reached: boolean) => void = () => undefined
    t.probe.mockImplementation(() => new Promise((resolve) => (answer = resolve)))
    t.monitor.checkNow()
    expect(t.monitor.getSnapshot()).toEqual({ status: 'serverUnreachable', checking: true })
    answer(true)
    await vi.advanceTimersByTimeAsync(0)
    expect(t.monitor.getSnapshot()).toEqual({ status: 'online', checking: false })
  })

  it('follows the browser offline event without polling, and checks when it comes back online', async () => {
    const t = setup()
    stop = t.stop
    t.setBrowserOnline(false)
    expect(t.status()).toBe('offline')
    await vi.advanceTimersByTimeAsync(60_000)
    expect(t.probe).not.toHaveBeenCalled()
    t.probe.mockResolvedValue(true)
    t.setBrowserOnline(true)
    await vi.advanceTimersByTimeAsync(0)
    expect(t.status()).toBe('online')
  })

  it('any successful call means the server is back', async () => {
    const t = setup()
    stop = t.stop
    await t.failQuery(networkError())
    await t.queryClient.getMutationCache().build(t.queryClient, { mutationFn: () => Promise.resolve('ok') }).execute(undefined)
    expect(t.status()).toBe('online')
  })

  it('notifies subscribers on change and gives TanStack its online state back when stopped', async () => {
    const t = setup()
    const listener = vi.fn<() => void>()
    t.monitor.subscribe(listener)
    await t.failQuery(networkError())
    expect(listener).toHaveBeenCalled()
    t.stop()
    window.dispatchEvent(new Event('online'))
    expect(onlineManager.isOnline()).toBe(true)
  })
})
