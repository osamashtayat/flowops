import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/apiError'
import { queryKeys } from '@/lib/api/queryKeys'
import { createQueryClient, shouldRetryQuery } from './queryClient'

const apiError = (status: number) => new ApiError({ status, code: 'x' })

describe('query retry rule', () => {
  it.each([400, 401, 403, 404, 409, 422, 429])('never retries a %i', (status) => {
    expect(shouldRetryQuery(0, apiError(status))).toBe(false)
  })

  it('retries a server error or network failure once', () => {
    expect(shouldRetryQuery(0, apiError(500))).toBe(true)
    expect(shouldRetryQuery(0, apiError(0))).toBe(true)
    expect(shouldRetryQuery(1, apiError(500))).toBe(false)
  })

  it('is wired into the client defaults, with no retries for mutations', () => {
    const defaults = createQueryClient().getDefaultOptions()
    expect(defaults.queries?.retry).toBe(shouldRetryQuery)
    expect(defaults.mutations?.retry).toBe(0)
    expect(defaults.queries?.staleTime).toBe(30_000)
  })
})

describe('queryKeys', () => {
  it('nests keys under their group prefix so a group can be invalidated at once', () => {
    expect(queryKeys.requests.detail('1').slice(0, 1)).toEqual(queryKeys.requests.all)
    expect(queryKeys.requests.list({ page: 1 }).slice(0, 2)).toEqual(queryKeys.requests.lists)
    expect(queryKeys.tasks.list({ status: 'Open' }).slice(0, 1)).toEqual(queryKeys.tasks.all)
    expect(queryKeys.notifications.unread.slice(0, 1)).toEqual(queryKeys.notifications.all)
  })

  it('includes params and ids so different views do not share a cache entry', () => {
    expect(queryKeys.requests.list({ page: 1 })).not.toEqual(queryKeys.requests.list({ page: 2 }))
    expect(queryKeys.requests.detail('a')).not.toEqual(queryKeys.requests.detail('b'))
  })
})
