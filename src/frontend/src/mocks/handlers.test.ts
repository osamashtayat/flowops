import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { setMockUser } from './state'
import { MOCK_RATE_LIMITED_EMAIL, MOCK_PASSWORD, MOCK_USERS, type MockUserKey } from './users'

async function rejection(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    if (error instanceof ApiError) return error
    throw error
  }
  throw new Error('expected the call to fail')
}

describe('mock auth', () => {
  it('serves the signed-in mock user from /me', async () => {
    const { data } = await api.GET('/api/auth/me')
    expect(data).toMatchObject({ email: 'employee@flowops.test', mustChangePassword: false })
  })

  it('returns 401 auth.unauthenticated when signed out', async () => {
    setMockUser(null)
    expect(await rejection(api.GET('/api/auth/me'))).toMatchObject({ status: 401, code: 'auth.unauthenticated' })
  })

  it('logs in with valid mock credentials and then /me returns that user', async () => {
    setMockUser(null)
    const { data } = await api.POST('/api/auth/login', { body: { email: 'Finance@flowops.test', password: MOCK_PASSWORD } })
    expect(data?.displayName).toBe(MOCK_USERS.finance.displayName)
    const me = await api.GET('/api/auth/me')
    expect(me.data?.email).toBe('finance@flowops.test')
  })

  it('rejects wrong credentials with 401 auth.invalid_credentials', async () => {
    setMockUser(null)
    const error = await rejection(api.POST('/api/auth/login', { body: { email: 'employee@flowops.test', password: 'nope' } }))
    expect(error).toMatchObject({ status: 401, code: 'auth.invalid_credentials' })
  })

  it('answers a rate-limited email with 429 auth.rate_limited', async () => {
    setMockUser(null)
    const error = await rejection(api.POST('/api/auth/login', { body: { email: MOCK_RATE_LIMITED_EMAIL, password: MOCK_PASSWORD } }))
    expect(error).toMatchObject({ status: 429, code: 'auth.rate_limited' })
  })

  it('logout signs out', async () => {
    await api.POST('/api/auth/logout')
    expect(await rejection(api.GET('/api/auth/me'))).toMatchObject({ status: 401 })
  })

  it('change-password maps validation onto field paths, then clears mustChangePassword', async () => {
    setMockUser('newHire')
    expect((await api.GET('/api/auth/me')).data?.mustChangePassword).toBe(true)

    const wrongCurrent = await rejection(
      api.POST('/api/auth/change-password', { body: { currentPassword: 'bad', newPassword: 'a-long-enough-password' } }),
    )
    expect(wrongCurrent).toMatchObject({ status: 400, code: 'validation', fieldErrors: { currentPassword: expect.any(Array) } })

    const tooShort = await rejection(
      api.POST('/api/auth/change-password', { body: { currentPassword: MOCK_PASSWORD, newPassword: 'short' } }),
    )
    expect(tooShort.fieldErrors).toHaveProperty('newPassword')

    await api.POST('/api/auth/change-password', { body: { currentPassword: MOCK_PASSWORD, newPassword: 'a-long-enough-password' } })
    expect((await api.GET('/api/auth/me')).data?.mustChangePassword).toBe(false)
  })
})

describe('mock users follow the capability model', () => {
  const expected: Array<[MockUserKey, string[], string[]]> = [
    ['employee', ['Employee'], ['requests.submit']],
    ['manager', ['Employee'], ['requests.submit', 'tasks.view']],
    ['finance', ['Employee', 'FinanceOfficer'], ['requests.submit', 'tasks.view']],
    ['admin', ['Administrator'], ['admin.manage']],
  ]

  it.each(expected)('%s has roles %j and capabilities %j', async (key, roles, capabilities) => {
    setMockUser(key)
    const { data } = await api.GET('/api/auth/me')
    expect(data?.roles).toEqual(roles)
    expect(data?.capabilities).toEqual(capabilities)
  })

  it('always sends nullable fields as null, never missing', async () => {
    setMockUser('admin')
    const { data } = await api.GET('/api/auth/me')
    expect(data).toHaveProperty('department', null)
    expect(data).toHaveProperty('manager', null)
  })
})

describe('mock inbox', () => {
  it('is forbidden without tasks.view', async () => {
    expect(await rejection(api.GET('/api/tasks'))).toMatchObject({ status: 403, code: 'auth.forbidden' })
  })

  it('returns open counts per user with paging fields', async () => {
    setMockUser('manager')
    const { data } = await api.GET('/api/tasks', { params: { query: { Status: 'Open', Page: 1, PageSize: 1 } } })
    expect(data).toMatchObject({ page: 1, pageSize: 1, totalCount: 3 })
    expect(data?.items).toHaveLength(1)
    setMockUser('finance')
    expect((await api.GET('/api/tasks')).data?.totalCount).toBe(5)
  })
})
