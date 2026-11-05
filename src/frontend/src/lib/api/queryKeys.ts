/**
 * Central TanStack Query keys (docs/UI_SPEC.md §6). Hooks build keys here only, so invalidation
 * by prefix is predictable: `queryKeys.requests.all` invalidates every request query.
 */
type Params = Readonly<Record<string, unknown>>

export const queryKeys = {
  session: ['session'] as const,
  dashboard: ['dashboard'] as const,
  requests: {
    all: ['requests'] as const,
    lists: ['requests', 'list'] as const,
    list: (params: Params) => ['requests', 'list', params] as const,
    detail: (id: string) => ['requests', 'detail', id] as const,
    activity: (id: string) => ['requests', 'activity', id] as const,
  },
  tasks: {
    all: ['tasks'] as const,
    list: (params: Params) => ['tasks', 'list', params] as const,
  },
  notifications: {
    all: ['notifications'] as const,
    list: (params: Params) => ['notifications', 'list', params] as const,
    unread: ['notifications', 'unread'] as const,
  },
  admin: {
    users: (params: Params) => ['admin', 'users', params] as const,
    user: (id: string) => ['admin', 'users', 'detail', id] as const,
    departments: (params: Params) => ['admin', 'departments', params] as const,
    stalled: (params: Params) => ['admin', 'stalled', params] as const,
  },
} as const
