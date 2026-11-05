import createClient, { type Middleware } from 'openapi-fetch'
import { ApiError, NETWORK_ERROR_CODE, apiErrorFromResponse } from './apiError'
import type { paths } from './schema'

/**
 * Antiforgery: the server sets this cookie (and refreshes it on GET /api/auth/me); unsafe requests
 * must echo it in the header (docs/API_CONTRACT.md, ADR-0003).
 * NOTE: the cookie name is the ASP.NET Core convention. The contract does not name it; confirm it
 * when BE-05 lands and change this one constant if it differs.
 */
export const XSRF_COOKIE_NAME = 'XSRF-TOKEN'
export const XSRF_HEADER_NAME = 'X-XSRF-TOKEN'

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE'])

function readCookie(name: string): string | null {
  for (const part of document.cookie.split('; ')) {
    const separator = part.indexOf('=')
    if (separator > 0 && part.slice(0, separator) === name) {
      return decodeURIComponent(part.slice(separator + 1))
    }
  }
  return null
}

const xsrfMiddleware: Middleware = {
  onRequest({ request }) {
    if (!SAFE_METHODS.has(request.method.toUpperCase())) {
      const token = readCookie(XSRF_COOKIE_NAME)
      if (token) request.headers.set(XSRF_HEADER_NAME, token)
    }
    return request
  },
}

/** Turns every non-2xx response into a thrown ApiError, so callers (TanStack Query) just `await`. */
const problemMiddleware: Middleware = {
  async onResponse({ response }) {
    if (!response.ok) throw await apiErrorFromResponse(response)
    return response
  },
}

/** The only place in the app that calls the global `fetch`. */
async function fetchOrThrowApiError(request: Request): Promise<Response> {
  try {
    return await fetch(request)
  } catch {
    // fetch only rejects when no response arrived at all.
    throw new ApiError({ status: 0, code: NETWORK_ERROR_CODE })
  }
}

export function createApiClient() {
  const client = createClient<paths>({
    // Same origin (the dev proxy / reverse proxy forwards /api). An absolute base keeps `Request`
    // construction valid outside the browser, e.g. in tests.
    baseUrl: window.location.origin,
    credentials: 'same-origin',
    fetch: fetchOrThrowApiError,
  })
  client.use(xsrfMiddleware, problemMiddleware)
  return client
}

/** Typed client for contracts/openapi.json. Resolves with `data`; rejects with `ApiError`. */
export const api = createApiClient()
