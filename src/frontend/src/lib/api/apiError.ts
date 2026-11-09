/** `code` used when the server's reply has no usable ProblemDetails (e.g. a proxy's HTML error page). */
export const UNKNOWN_ERROR_CODE = 'unknown'
/** `code` used when the request never reached the server (offline, DNS, connection refused). */
export const NETWORK_ERROR_CODE = 'network.unreachable'

export interface ApiErrorInit {
  status: number
  code: string
  traceId?: string | null
  /** camelCase field path → messages, from a 400 `errors` object (docs/API_CONTRACT.md §2). */
  fieldErrors?: Record<string, string[]>
}

/**
 * A failed API call. `code` is the stable machine-readable code from the ProblemDetails body
 * (`task.already_decided`…); screens show `toUserMessage(error)`, never the server's `detail`.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly traceId: string | null
  readonly fieldErrors: Record<string, string[]>

  constructor(init: ApiErrorInit) {
    super(`${init.status} ${init.code}`)
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.traceId = init.traceId ?? null
    this.fieldErrors = init.fieldErrors ?? {}
  }
}

/** A call that must answer with a body (200, or 201 for a create) but did not: a failure, never a silent success. */
export function requireData<T>(data: T | undefined, status = 200): T {
  if (data === undefined) throw new ApiError({ status, code: UNKNOWN_ERROR_CODE })
  return data
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function parseFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) return {}
  const result: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      const texts = messages.filter((m): m is string => typeof m === 'string')
      if (texts.length > 0) result[field] = texts
    }
  }
  return result
}

/** Builds an ApiError from a non-2xx response, tolerating bodies that are not ProblemDetails. */
export async function apiErrorFromResponse(response: Response): Promise<ApiError> {
  let body: unknown = null
  try {
    body = await response.json()
  } catch {
    // Not JSON (empty body, HTML from a proxy): fall through with the status only.
  }
  const problem = isRecord(body) ? body : {}
  return new ApiError({
    status: response.status,
    code: typeof problem.code === 'string' ? problem.code : UNKNOWN_ERROR_CODE,
    traceId: typeof problem.traceId === 'string' ? problem.traceId : null,
    fieldErrors: parseFieldErrors(problem.errors),
  })
}
