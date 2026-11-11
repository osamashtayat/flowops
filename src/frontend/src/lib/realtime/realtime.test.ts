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

  it('waits for a connection created asynchronously, and retries when creating it fails', async () => {
    const { client, keys } = spyClient()
    const hub = new FakeHub()
    let creations = 0
    const createConnection = () => {
      creations += 1
      return creations === 1 ? Promise.reject(new Error('chunk failed to load')) : Promise.resolve(hub)
    }
    startRealtime(client, { createConnection, retryDelaysMs: [1000] })
    await vi.advanceTimersByTimeAsync(0)
    expect(hub.starts).toBe(0)
    await vi.advanceTimersByTimeAsync(1000)
    expect(creations).toBe(2)
    expect(hub.starts).toBe(1)
    hub.handlers.get('tasksChanged')?.({})
    expect(keys()).toContainEqual(queryKeys.tasks.all)
  })

  it('never starts a connection that finished creating after it was stopped', async () => {
    const { client } = spyClient()
    const hub = new FakeHub()
    const stop = startRealtime(client, { createConnection: () => Promise.resolve(hub) })
    stop()
    await vi.advanceTimersByTimeAsync(0)
    expect(hub.starts).toBe(0)
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

  it('stops retrying while the server is unreachable and reconnects as soon as it is back', async () => {
    const { client, keys } = spyClient()
    const hub = new FakeHub()
    const listeners = new Set<(online: boolean) => void>()
    let isOnline = false
    const online = {
      isOnline: () => isOnline,
      subscribe: (listener: (online: boolean) => void) => {
        listeners.add(listener)
        return () => listeners.delete(listener)
      },
    }
    hub.failStarts = 1
    startRealtime(client, { createConnection: () => hub, retryDelaysMs: [1000], online })
    await vi.advanceTimersByTimeAsync(60_000)
    expect(hub.starts).toBe(1) // no retries while offline
    isOnline = true
    for (const listener of listeners) listener(true)
    await vi.advanceTimersByTimeAsync(0)
    expect(hub.starts).toBe(2)
    expect(keys()).toContainEqual(queryKeys.tasks.all) // refetches what it missed
  })
})
