import { QueryClient } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { queryKeys } from '@/lib/api/queryKeys'
import { startRealtime, type HubLike } from './connection'
import { invalidateAllRealtime, invalidateForEvent } from './invalidation'

function spyClient() {
  const client = new QueryClient()
  const spy = vi.spyOn(client, 'invalidateQueries').mockResolvedValue()
  const keys = () => spy.mock.calls.map(([filters]) => filters?.queryKey)
  return { client, keys }
}

describe('invalidateForEvent (API_CONTRACT §9)', () => {
  it('notificationCreated → notifications and dashboard', () => {
    const { client, keys } = spyClient()
    invalidateForEvent(client, 'notificationCreated', { notificationId: 'n1', requestId: null })
    expect(keys()).toEqual([queryKeys.notifications.all, queryKeys.dashboard])
  })

  it('requestChanged → that request, its activity, request lists and dashboard', () => {
    const { client, keys } = spyClient()
    invalidateForEvent(client, 'requestChanged', { requestId: 'r1' })
    expect(keys()).toEqual([queryKeys.requests.detail('r1'), queryKeys.requests.activity('r1'), queryKeys.requests.lists, queryKeys.dashboard])
  })

  it('requestChanged with an unusable payload refetches all request queries instead of guessing', () => {
    for (const payload of [null, {}, { requestId: 5 }, { requestId: '' }, 'x']) {
      const { client, keys } = spyClient()
      invalidateForEvent(client, 'requestChanged', payload)
      expect(keys()).toEqual([queryKeys.requests.all, queryKeys.dashboard])
    }
  })

  it('tasksChanged → tasks and dashboard', () => {
    const { client, keys } = spyClient()
    invalidateForEvent(client, 'tasksChanged', {})
    expect(keys()).toEqual([queryKeys.tasks.all, queryKeys.dashboard])
  })

  it('a reconnect refetches everything the events cover', () => {
    const { client, keys } = spyClient()
    invalidateAllRealtime(client)
    expect(keys()).toEqual([queryKeys.notifications.all, queryKeys.requests.all, queryKeys.tasks.all, queryKeys.dashboard])
  })
})

class FakeHub implements HubLike {
  handlers = new Map<string, (payload: unknown) => void>()
  reconnected: () => void = () => undefined
  closed: () => void = () => undefined
  starts = 0
  stops = 0
  failStarts = 0
  on(event: string, handler: (payload: unknown) => void) {
    this.handlers.set(event, handler)
  }
  onreconnected(handler: () => void) {
    this.reconnected = handler
  }
  onclose(handler: () => void) {
    this.closed = handler
  }
  start() {
    this.starts += 1
    if (this.failStarts > 0) {
      this.failStarts -= 1
      return Promise.reject(new Error('negotiate failed'))
    }
    return Promise.resolve()
  }
  stop() {
    this.stops += 1
    return Promise.resolve()
  }
}

describe('startRealtime', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('maps hub events to invalidations and stops cleanly', async () => {
    const { client, keys } = spyClient()
    const hub = new FakeHub()
    const stop = startRealtime(client, { createConnection: () => hub })
    await vi.advanceTimersByTimeAsync(0)
    expect(keys()).toEqual([]) // a first, clean connect refetches nothing
    hub.handlers.get('tasksChanged')?.({})
    expect(keys()).toEqual([queryKeys.tasks.all, queryKeys.dashboard])
    stop()
    expect(hub.stops).toBe(1)
  })

  it('invalidates everything after SignalR reconnects', async () => {
    const { client, keys } = spyClient()
    const hub = new FakeHub()
    startRealtime(client, { createConnection: () => hub })
    await vi.advanceTimersByTimeAsync(0)
    hub.reconnected()
    expect(keys()).toContainEqual(queryKeys.notifications.all)
  })

  it('retries a failed start quietly with the given delays, then refetches what it missed', async () => {
    const { client, keys } = spyClient()
    const hub = new FakeHub()
    hub.failStarts = 2
    startRealtime(client, { createConnection: () => hub, retryDelaysMs: [1000, 5000] })
    await vi.advanceTimersByTimeAsync(0)
    expect(hub.starts).toBe(1)
    await vi.advanceTimersByTimeAsync(999)
    expect(hub.starts).toBe(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(hub.starts).toBe(2)
    await vi.advanceTimersByTimeAsync(5000)
    expect(hub.starts).toBe(3)
    expect(keys()).toContainEqual(queryKeys.tasks.all)
  })

  it('starts over when SignalR gives up, and does nothing once stopped', async () => {
    const { client } = spyClient()
    const hub = new FakeHub()
    const stop = startRealtime(client, { createConnection: () => hub, retryDelaysMs: [1000] })
    await vi.advanceTimersByTimeAsync(0)
    hub.closed()
    await vi.advanceTimersByTimeAsync(1000)
    expect(hub.starts).toBe(2)
    stop()
    hub.closed()
    await vi.advanceTimersByTimeAsync(10_000)
    expect(hub.starts).toBe(2)
  })
})
