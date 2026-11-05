import type { RouteObject } from 'react-router'
import { FoundationPage } from '@/app/FoundationPage'
import { RootLayout } from '@/app/RootLayout'
import { ForbiddenPage } from '@/app/pages/ForbiddenPage'
import { NotFoundPage } from '@/app/pages/NotFoundPage'
import { AppShell } from '@/components/layout/AppShell'
import { RequireAuth } from '@/features/session/components/RequireAuth'
import { ChangePasswordPage } from '@/features/session/pages/ChangePasswordPage'
import { LoginPage } from '@/features/session/pages/LoginPage'

/**
 * Route table (docs/UI_SPEC.md §1). Grows per feature; capability-guarded groups use
 * `<RequireCapability capability="…" />` as a layout route.
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
          { element: <AppShell />, children: [{ index: true, element: <FoundationPage /> }] },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]
