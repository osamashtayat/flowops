import type { RouteObject } from 'react-router'
import { FoundationPage } from '@/app/FoundationPage'
import { PlaceholderPage } from '@/app/PlaceholderPage'
import { RootLayout } from '@/app/RootLayout'
import { ForbiddenPage } from '@/app/pages/ForbiddenPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
import { AppShell } from '@/components/layout/AppShell'
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
              { index: true, element: <FoundationPage /> },
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
                  { path: '/admin/users', element: <PlaceholderPage title="Users" task="FE-13" /> },
                  { path: '/admin/departments', element: <PlaceholderPage title="Departments" task="FE-13" /> },
                  { path: '/admin/stalled-tasks', element: <PlaceholderPage title="Stalled tasks" task="FE-13" /> },
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
