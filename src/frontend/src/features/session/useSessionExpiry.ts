import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useLocation, useNavigate } from 'react-router'
import { useToast } from '@/components/ui/toastContext'
import { ApiError } from '@/lib/api/apiError'
import { queryKeys } from '@/lib/api/queryKeys'
import { PROBLEM_MESSAGES } from '@/lib/api/problem'
import { LOGIN_PATH, loginPath } from './returnTo'

/** A 401 on any call except a wrong password at sign-in means the session ended mid-use. */
function isSessionExpired(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401 && error.code !== 'auth.invalid_credentials'
}

/**
 * Any API 401 mid-session → clear the cache, tell the user, go to /login?returnTo=<here>
 * (docs/UI_SPEC.md §2). Watches every query and mutation, so no screen handles it itself.
 */
export function useSessionExpiry() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const toast = useToast()
  const location = useLocation()
  const hereRef = useRef(location.pathname + location.search)
  const handlingRef = useRef(false)

  useEffect(() => {
    hereRef.current = location.pathname + location.search
    // Back on the sign-in page: a later expiry is a new event.
    if (location.pathname === LOGIN_PATH) handlingRef.current = false
  }, [location.pathname, location.search])

  useEffect(() => {
    function onError(error: unknown) {
      if (handlingRef.current || !isSessionExpired(error)) return
      handlingRef.current = true
      const returnTo = hereRef.current
      queryClient.clear()
      queryClient.setQueryData(queryKeys.session, null)
      toast.info(PROBLEM_MESSAGES['auth.unauthenticated'])
      void navigate(loginPath(returnTo), { replace: true })
    }
    const stopQueries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === 'updated' && event.action.type === 'error') onError(event.action.error)
    })
    const stopMutations = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === 'updated' && event.action.type === 'error') onError(event.action.error)
    })
    return () => {
      stopQueries()
      stopMutations()
    }
  }, [queryClient, navigate, toast])
}
