import type { ComponentType } from 'react'
import type { RouteObject } from 'react-router'
import { RootLayout } from '@/app/RootLayout'
import { ForbiddenPage } from '@/app/pages/ForbiddenPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
import { AppShell } from '@/components/layout/AppShell'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { RequireAuth } from '@/features/session/components/RequireAuth'
import { RequireCapability } from '@/features/session/components/RequireCapability'

/**
 * A page loaded on first visit, in its own chunk (issue 101): the first screen downloads only the shell and
 * the dashboard, not every form, admin page and their libraries (zod, react-hook-form).
 */
function page<M>(load: () => Promise<M>, pick: (module: M) => ComponentType): RouteObject['lazy'] {
  return async () => ({ Component: pick(await load()) })
}

/**
 * Route table (docs/UI_SPEC.md §1), with the same guards as the spec: sign-in for everything
 * behind `RequireAuth`, and a capability per group. Guards, the shell and the dashboard (where
 * sign-in lands) load with the app; every other page is lazy.
 */
export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    // Shown only while a lazy page of the first URL loads; the HTML is already blank until then.
    hydrateFallbackElement: null,
    children: [
      { path: '/login', lazy: page(() => import('@/features/session/pages/LoginPage'), (m) => m.LoginPage) },
      { path: '/forbidden', element: <ForbiddenPage /> },
      {
        element: <RequireAuth />,
        children: [
          {
            path: '/change-password',
            lazy: page(() => import('@/features/session/pages/ChangePasswordPage'), (m) => m.ChangePasswordPage),
          },
          {
            element: <AppShell />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: '/requests', lazy: page(() => import('@/features/requests/pages/MyRequestsPage'), (m) => m.MyRequestsPage) },
              {
                path: '/notifications',
                lazy: page(() => import('@/features/notifications/pages/NotificationsPage'), (m) => m.NotificationsPage),
              },
              {
                path: '/requests/:id',
                lazy: page(() => import('@/features/requests/pages/RequestDetailsPage'), (m) => m.RequestDetailsPage),
              },
              {
                element: <RequireCapability capability="requests.submit" />,
                children: [
                  { path: '/requests/new', lazy: page(() => import('@/features/requests/pages/NewRequestPage'), (m) => m.NewRequestPage) },
                  {
                    path: '/requests/new/purchase',
                    lazy: page(() => import('@/features/requests/pages/NewPurchasePage'), (m) => m.NewPurchasePage),
                  },
                  { path: '/requests/new/leave', lazy: page(() => import('@/features/requests/pages/NewLeavePage'), (m) => m.NewLeavePage) },
                ],
              },
              {
                element: <RequireCapability capability="tasks.view" />,
                children: [
                  {
                    path: '/requests/team',
                    lazy: page(() => import('@/features/requests/pages/TeamRequestsPage'), (m) => m.TeamRequestsPage),
                  },
                  { path: '/inbox', lazy: page(() => import('@/features/tasks/pages/InboxPage'), (m) => m.InboxPage) },
                ],
              },
              {
                element: <RequireCapability capability="admin.manage" />,
                children: [
                  { path: '/admin/users', lazy: page(() => import('@/features/admin/pages/UsersPage'), (m) => m.UsersPage) },
                  { path: '/admin/users/new', lazy: page(() => import('@/features/admin/pages/UserFormPage'), (m) => m.NewUserPage) },
                  { path: '/admin/users/:id', lazy: page(() => import('@/features/admin/pages/UserFormPage'), (m) => m.EditUserPage) },
                  {
                    path: '/admin/departments',
                    lazy: page(() => import('@/features/admin/pages/DepartmentsPage'), (m) => m.DepartmentsPage),
                  },
                  {
                    path: '/admin/role-changes',
                    lazy: page(() => import('@/features/admin/pages/RoleChangesPage'), (m) => m.RoleChangesPage),
                  },
                  {
                    path: '/admin/stalled-tasks',
                    lazy: page(() => import('@/features/admin/pages/StalledTasksPage'), (m) => m.StalledTasksPage),
                  },
                ],
              },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
