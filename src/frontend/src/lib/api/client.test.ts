import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it } from 'vitest'
import { server } from '@/mocks/server'
import { ApiError, NETWORK_ERROR_CODE, UNKNOWN_ERROR_CODE } from './apiError'
import { api, XSRF_COOKIE_NAME, XSRF_HEADER_NAME } from './client'

function setXsrfCookie(value: string | null) {
  document.cookie = value
    ? `${XSRF_COOKIE_NAME}=${value}; path=/`
    : `${XSRF_COOKIE_NAME}=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`
}

afterEach(() => setXsrfCookie(null))

/** Registers a handler that records the XSRF header it received. */
function captureXsrf(method: 'get' | 'post' | 'put', path: string) {
  const seen: Array<string | null> = []
  server.use(
    http[method](path, ({ request }) => {
      seen.push(request.headers.get(XSRF_HEADER_NAME))
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return seen
}

describe('XSRF header', () => {
  it('is attached to unsafe methods when the cookie exists', async () => {
    setXsrfCookie('token-123')
    const seen = captureXsrf('post', '/api/auth/logout')
    await api.POST('/api/auth/logout')
    expect(seen).toEqual(['token-123'])
  })

  it('is attached to PUT as well', async () => {
    setXsrfCookie('abc')
    const seen = captureXsrf('put', '/api/users/{id}/roles'.replace('{id}', 'u1'))
    await api.PUT('/api/users/{id}/roles', { params: { path: { id: 'u1' } }, body: { roles: ['Employee'] } })
    expect(seen).toEqual(['abc'])
  })

  it('is NOT attached to safe methods', async () => {
    setXsrfCookie('token-123')
    const seen: Array<string | null> = []
    server.use(
      http.get('/api/auth/me', ({ request }) => {
        seen.push(request.headers.get(XSRF_HEADER_NAME))
        return HttpResponse.json({}, { status: 200 })
      }),
    )
    await api.GET('/api/auth/me')
    expect(seen).toEqual([null])
  })

  it('is omitted when there is no cookie yet (e.g. login)', async () => {
    const seen = captureXsrf('post', '/api/auth/logout')
    await api.POST('/api/auth/logout')
    expect(seen).toEqual([null])
  })

  it('decodes the cookie value', async () => {
    setXsrfCookie('a%2Fb%3D')
    const seen = captureXsrf('post', '/api/auth/logout')
    await api.POST('/api/auth/logout')
    expect(seen).toEqual(['a/b='])
  })
})

describe('errors', () => {
  async function failure(): Promise<unknown> {
    try {
      await api.GET('/api/auth/me')
    } catch (error) {
      return error
    }
    return undefined
  }

  it('resolves with typed data on success', async () => {
    const { data } = await api.GET('/api/auth/me')
    expect(data?.email).toBe('employee@flowops.test')
  })

  it('rejects with an ApiError carrying status, code and traceId from ProblemDetails', async () => {
    server.use(
      http.get('/api/auth/me', () =>
        HttpResponse.json(
          { status: 409, code: 'task.already_decided', traceId: 'trace-1', title: null, detail: null, type: null, instance: null },
          { status: 409 },
        ),
      ),
    )
    const error = await failure()
    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, code: 'task.already_decided', traceId: 'trace-1', fieldErrors: {} })
  })

  it('keeps 400 field errors keyed by camelCase path', async () => {
    server.use(
      http.get('/api/auth/me', () =>
        HttpResponse.json(
          { code: 'validation', traceId: 't', errors: { 'estimatedCost.amount': ['Too large.', 'Second'], itemName: ['Required.'], bad: 'nope' } },
          { status: 400 },
        ),
      ),
    )
    const error = (await failure()) as ApiError
    expect(error.fieldErrors).toEqual({ 'estimatedCost.amount': ['Too large.', 'Second'], itemName: ['Required.'] })
  })

  it('falls back to code "unknown" when the body is not ProblemDetails (HTML from a proxy)', async () => {
    server.use(http.get('/api/auth/me', () => new HttpResponse('<html>Bad gateway</html>', { status: 502 })))
    const error = (await failure()) as ApiError
    expect(error).toMatchObject({ status: 502, code: UNKNOWN_ERROR_CODE, traceId: null })
  })

  it('falls back to "unknown" for an empty body', async () => {
    server.use(http.get('/api/auth/me', () => new HttpResponse(null, { status: 501 })))
    expect(await failure()).toMatchObject({ status: 501, code: UNKNOWN_ERROR_CODE })
  })

  it('turns a network failure into an ApiError with status 0', async () => {
    server.use(http.get('/api/auth/me', () => HttpResponse.error()))
    expect(await failure()).toMatchObject({ status: 0, code: NETWORK_ERROR_CODE })
  })
})
