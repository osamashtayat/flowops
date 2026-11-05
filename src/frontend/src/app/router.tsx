import { createBrowserRouter } from 'react-router'
import { AppShell } from '@/components/layout/AppShell'
import { FoundationPage } from '@/app/FoundationPage'

// Route table grows per docs/UI_SPEC.md §1 (guards added in FE-03).
export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [{ index: true, element: <FoundationPage /> }],
  },
])
