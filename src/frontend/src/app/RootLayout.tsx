import { lazy, Suspense } from 'react'
import { Outlet } from 'react-router'
import { useSessionExpiry } from '@/features/session/useSessionExpiry'

// Development-only role switcher for the mock API. `import.meta.env.MODE` is a build-time
// constant, so production builds drop this branch and never emit the chunk; tests (mode "test")
// don't render it.
const MockUserSwitcher =
  import.meta.env.MODE === 'development' && import.meta.env.VITE_API_MOCKS !== 'off'
    ? lazy(() => import('@/mocks/MockUserSwitcher'))
    : null

/** Top of the route tree: app-wide behaviour that must work on every page, signed in or not. */
export function RootLayout() {
  useSessionExpiry()
  return (
    <>
      <Outlet />
      {MockUserSwitcher && (
        <Suspense fallback={null}>
          <MockUserSwitcher />
        </Suspense>
      )}
    </>
  )
}
