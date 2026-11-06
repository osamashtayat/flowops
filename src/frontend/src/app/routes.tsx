import type { RouteObject } from 'react-router'
import { PlaceholderPage } from '@/app/PlaceholderPage'
import { RootLayout } from '@/app/RootLayout'
import { ForbiddenPage } from '@/app/pages/ForbiddenPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
import { AppShell } from '@/components/layout/AppShell'
import { DepartmentsPage } from '@/features/admin/pages/DepartmentsPage'
import { StalledTasksPage } from '@/features/admin/pages/StalledTasksPage'
import { EditUserPage, NewUserPage } from '@/features/admin/pages/UserFormPage'
import { UsersPage } from '@/features/admin/pages/UsersPage'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { MyRequestsPage } from '@/features/requests/pages/MyRequestsPage'
import { NewLeavePage } from '@/features/requests/pages/NewLeavePage'
import { NewPurchasePage } from '@/features/requests/pages/NewPurchasePage'
import { NewRequestPage } from '@/features/requests/pages/NewRequestPage'
import { RequestDetailsPage } from '@/features/requests/pages/RequestDetailsPage'
import { TeamRequestsPage } from '@/features/requests/pages/TeamRequestsPage'
import { RequireAuth } from '@/features/session/components/RequireAuth'
import { RequireCapability } from '@/features/session/components/RequireCapability'
import { ChangePasswordPage } from '@/features/session/pages/ChangePasswordPage'
import { LoginPage } from '@/features/session/pages/LoginPage'

/**
 * Route table (docs/UI_SPEC.md §1), with the same guards as the spec: sign-in for everything
 * behind `RequireAuth`, and a capability per group. `PlaceholderPage` marks screens a later task
 * builds; replace the element, keep the guard.
 */
export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/forbidden', element: <ForbiddenPage /> },
      {
        element: <RequireAuth />,
        children: [
          { path: '/change-password', element: <ChangePasswordPage /> },
          {
            element: <AppShell />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: '/requests', element: <MyRequestsPage /> },
              { path: '/notifications', element: <PlaceholderPage title="Notifications" task="FE-11" /> },
              { path: '/requests/:id', element: <RequestDetailsPage /> },
              {
                element: <RequireCapability capability="requests.submit" />,
                children: [
                  { path: '/requests/new', element: <NewRequestPage /> },
                  { path: '/requests/new/purchase', element: <NewPurchasePage /> },
                  { path: '/requests/new/leave', element: <NewLeavePage /> },
                ],
              },
              {
                element: <RequireCapability capability="tasks.view" />,
                children: [
                  { path: '/requests/team', element: <TeamRequestsPage /> },
                  { path: '/inbox', element: <PlaceholderPage title="Inbox" task="FE-10" /> },
                ],
              },
              {
                element: <RequireCapability capability="admin.manage" />,
                children: [
                  { path: '/admin/users', element: <UsersPage /> },
                  { path: '/admin/users/new', element: <NewUserPage /> },
                  { path: '/admin/users/:id', element: <EditUserPage /> },
                  { path: '/admin/departments', element: <DepartmentsPage /> },
                  { path: '/admin/stalled-tasks', element: <StalledTasksPage /> },
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
