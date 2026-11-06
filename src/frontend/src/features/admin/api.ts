import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type {
  CreateDepartmentRequest,
  CreateUserRequest,
  DepartmentDto,
  Role,
  UpdateDepartmentRequest,
  UpdateUserRequest,
  UserDto,
} from '@/lib/api/types'

/** A write that should answer with a body (201/200) but did not: treat as a failure, not as success. */
function required<T>(data: T | undefined): T {
  if (data === undefined) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
  return data
}

export type UserListParams = {
  search: string
  departmentId: string
  role: Role | ''
  includeInactive: boolean
  page: number
  pageSize: number
}

/** `GET /api/users`. Empty filters are left out; the previous page stays visible while the next loads. */
export function useUsers(params: UserListParams) {
  return useQuery({
    queryKey: queryKeys.admin.users(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/users', {
        params: {
          query: {
            search: params.search || undefined,
            departmentId: params.departmentId || undefined,
            role: params.role || undefined,
            includeInactive: params.includeInactive || undefined,
            page: params.page,
            pageSize: params.pageSize,
          },
        },
      })
      return required(data)
    },
    placeholderData: keepPreviousData,
  })
}

export function useUser(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.admin.user(id ?? ''),
    queryFn: async () => {
      const { data } = await api.GET('/api/users/{id}', { params: { path: { id: id ?? '' } } })
      return required(data)
    },
    enabled: id !== undefined,
  })
}

/** `GET /api/departments`. Admins see inactive ones too when asked. */
export function useDepartments(includeInactive: boolean) {
  const params = { includeInactive }
  return useQuery({
    queryKey: queryKeys.admin.departments(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/departments', { params: { query: params } })
      return required(data)
    },
  })
}

/** Any user change can alter lists, details, department member counts and the dashboard's admin counts. */
function useRefreshAfterUserChange() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.admin.usersAll }),
      queryClient.invalidateQueries({ queryKey: queryKeys.admin.departmentsAll }),
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
    ])
}

export function useCreateUser() {
  const refresh = useRefreshAfterUserChange()
  return useMutation({
    mutationFn: async (body: CreateUserRequest): Promise<UserDto> => {
      const { data } = await api.POST('/api/users', { body })
      return required(data)
    },
    onSuccess: refresh,
  })
}

export interface SaveUserInput {
  id: string
  profile: UpdateUserRequest
  /** Sent as a second call only when given (the admin changed the roles). */
  roles?: Role[]
}

/**
 * Saves the profile (`PUT /api/users/{id}`) and, when the roles changed, the roles
 * (`PUT /api/users/{id}/roles`). They are two endpoints, so a failure of the second leaves the
 * first saved; the caller refetches and shows the server's message.
 */
export function useSaveUser() {
  const refresh = useRefreshAfterUserChange()
  return useMutation({
    mutationFn: async ({ id, profile, roles }: SaveUserInput): Promise<UserDto> => {
      const params = { path: { id } }
      const saved = required((await api.PUT('/api/users/{id}', { params, body: profile })).data)
      if (!roles) return saved
      return required((await api.PUT('/api/users/{id}/roles', { params, body: { roles } })).data)
    },
    onSettled: refresh,
  })
}

export function useResetPassword() {
  const refresh = useRefreshAfterUserChange()
  return useMutation({
    mutationFn: async ({ id, newPassword }: { id: string; newPassword: string }) => {
      await api.POST('/api/users/{id}/reset-password', { params: { path: { id } }, body: { newPassword } })
    },
    onSuccess: refresh,
  })
}

/** A department change (rename, deactivate) also changes the department shown on users. */
function useRefreshAfterDepartmentChange() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.admin.departmentsAll }),
      queryClient.invalidateQueries({ queryKey: queryKeys.admin.usersAll }),
    ])
}

export function useCreateDepartment() {
  const refresh = useRefreshAfterDepartmentChange()
  return useMutation({
    mutationFn: async (body: CreateDepartmentRequest): Promise<DepartmentDto> => {
      const { data } = await api.POST('/api/departments', { body })
      return required(data)
    },
    onSuccess: refresh,
  })
}

export function useUpdateDepartment() {
  const refresh = useRefreshAfterDepartmentChange()
  return useMutation({
    mutationFn: async ({ id, ...body }: UpdateDepartmentRequest & { id: string }): Promise<DepartmentDto> => {
      const { data } = await api.PUT('/api/departments/{id}', { params: { path: { id } }, body })
      return required(data)
    },
    onSuccess: refresh,
  })
}

export function useStalledTasks(params: { page: number; pageSize: number }) {
  return useQuery({
    queryKey: queryKeys.admin.stalled(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/admin/stalled-tasks', { params: { query: params } })
      return required(data)
    },
    placeholderData: keepPreviousData,
  })
}
