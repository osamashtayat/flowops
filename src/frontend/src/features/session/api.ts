import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { ApiError } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type { ChangePasswordRequest, CurrentUserDto, LoginRequest } from '@/lib/api/types'
import { LOGIN_PATH } from './returnTo'

/** `null` means "nobody is signed in" — a normal state, not an error. */
async function fetchSession(): Promise<CurrentUserDto | null> {
  try {
    const { data } = await api.GET('/api/auth/me')
    return data ?? null
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null
    throw error
  }
}

export type SessionStatus = 'loading' | 'authenticated' | 'anonymous' | 'error'

/** The signed-in user (from `GET /api/auth/me`) and what the app may show them. */
export function useSession() {
  const query = useQuery({ queryKey: queryKeys.session, queryFn: fetchSession })
  const user = query.data ?? null
  const status: SessionStatus = query.isPending ? 'loading' : query.isError ? 'error' : user ? 'authenticated' : 'anonymous'
  return { status, user, error: query.error, refetch: query.refetch }
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: LoginRequest): Promise<CurrentUserDto> => {
      const { data } = await api.POST('/api/auth/login', { body })
      if (!data) throw new ApiError({ status: 200, code: 'unknown' })
      return data
    },
    onSuccess: (user) => queryClient.setQueryData(queryKeys.session, user),
  })
}

export function useChangePassword() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: ChangePasswordRequest) => {
      await api.POST('/api/auth/change-password', { body })
    },
    // Re-read the user: `mustChangePassword` is now false.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.session }),
  })
}

/** Signs out. Whatever the server says, the local session ends: cache cleared, back to /login. */
export function useLogout() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/auth/logout')
    },
    onSettled: () => {
      queryClient.clear()
      queryClient.setQueryData(queryKeys.session, null)
      void navigate(LOGIN_PATH, { replace: true })
    },
  })
}
