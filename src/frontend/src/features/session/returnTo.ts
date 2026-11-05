export const LOGIN_PATH = '/login'
export const CHANGE_PASSWORD_PATH = '/change-password'
export const FORBIDDEN_PATH = '/forbidden'
const HOME_PATH = '/'

/**
 * `returnTo` comes from the URL, so it is untrusted: only a same-origin path is allowed
 * (`/requests?page=2`). Anything else — `//evil.com`, `https://…`, `/\evil.com` — becomes `/`,
 * so a crafted login link cannot redirect users off-site after they sign in.
 */
export function safeReturnTo(raw: string | null | undefined): string {
  if (!raw || !raw.startsWith('/') || raw.startsWith('//') || raw.startsWith('/\\')) return HOME_PATH
  const path = raw.split(/[?#]/)[0]
  if (path === LOGIN_PATH) return HOME_PATH
  return raw
}

function withReturnTo(path: string, returnTo: string | null | undefined): string {
  const target = safeReturnTo(returnTo)
  return target === HOME_PATH ? path : `${path}?returnTo=${encodeURIComponent(target)}`
}

export const loginPath = (returnTo?: string | null) => withReturnTo(LOGIN_PATH, returnTo)
export const changePasswordPath = (returnTo?: string | null) => withReturnTo(CHANGE_PASSWORD_PATH, returnTo)
