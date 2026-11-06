import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, UNKNOWN_ERROR_CODE } from '@/lib/api/apiError'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import type { TaskAction, WorkTaskStatus } from '@/lib/api/types'

const OPEN_COUNT_PARAMS = { status: 'Open', page: 1, pageSize: 1 } as const

/**
 * How many tasks wait for the signed-in user (the sidebar's Inbox badge): `totalCount` of a
 * one-row page of `GET /api/tasks?status=Open`. Pass `enabled: false` without `tasks.view`.
 */
export function useOpenTaskCount(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.tasks.list(OPEN_COUNT_PARAMS),
    queryFn: async () => {
      const { data } = await api.GET('/api/tasks', { params: { query: OPEN_COUNT_PARAMS } })
      return data?.totalCount ?? 0
    },
    enabled,
  })
}

export type InboxParams = {
  status: WorkTaskStatus
  page: number
  pageSize: number
}

/** `GET /api/tasks`: Open = what the user can decide now, Done = what they decided. Keeps the previous page visible while the next loads. */
export function useInbox(params: InboxParams) {
  return useQuery({
    queryKey: queryKeys.tasks.list(params),
    queryFn: async () => {
      const { data } = await api.GET('/api/tasks', { params: { query: params } })
      if (!data) throw new ApiError({ status: 200, code: UNKNOWN_ERROR_CODE })
      return data
    },
    placeholderData: keepPreviousData,
  })
}

export interface DecideTaskInput {
  taskId: string
  action: TaskAction
  /** The `version` the server sent with `availableActions` / the inbox item. */
  version: string
  /** Approve comment, reject reason or fulfil note; "" means none. */
  text: string
}

async function decide({ taskId, action, version, text }: DecideTaskInput): Promise<void> {
  const params = { path: { taskId } }
  const optional = text === '' ? null : text
  switch (action) {
    case 'approve':
      await api.POST('/api/tasks/{taskId}/approve', { params, body: { version, comment: optional } })
      return
    case 'reject':
      await api.POST('/api/tasks/{taskId}/reject', { params, body: { version, reason: text } })
      return
    case 'fulfill':
      await api.POST('/api/tasks/{taskId}/fulfill', { params, body: { version, note: optional } })
      return
    default:
      return action satisfies never
  }
}

/**
 * The one decision mutation (approve / reject / fulfil), used by the request page and the inbox.
 * Whatever the outcome — success, 409 "someone else got there first", 403, 404 — what the screen
 * showed may be stale, so tasks, requests and the dashboard are refetched. A conflict is never
 * retried silently (.claude/rules/security.md, "Concurrency as security").
 */
export function useDecideTask() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: decide,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.tasks.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.requests.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ]),
  })
}
