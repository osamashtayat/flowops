import { Navigate, Outlet, useLocation } from 'react-router'
import { useSession } from '../api'
import { CHANGE_PASSWORD_PATH, changePasswordPath, loginPath } from '../returnTo'
import { SessionError, SessionLoading } from './SessionGate'

/**
 * Layout route for everything behind sign-in (docs/UI_SPEC.md §2):
 * signed out → /login?returnTo=<here>; must change password → only /change-password is allowed.
 */
export function RequireAuth() {
  const { status, user, error, refetch } = useSession()
  const location = useLocation()
  const here = location.pathname + location.search

  if (status === 'loading') return <SessionLoading />
  if (status === 'error') return <SessionError error={error} onRetry={() => void refetch()} />
  if (!user) return <Navigate to={loginPath(here)} replace />
  if (user.mustChangePassword && location.pathname !== CHANGE_PASSWORD_PATH) {
    return <Navigate to={changePasswordPath(here)} replace />
  }
  return <Outlet />
}
