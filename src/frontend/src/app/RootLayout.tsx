import { Outlet } from 'react-router'
import { ConnectionBanner } from '@/components/layout/ConnectionBanner'
import { useSessionExpiry } from '@/features/session/useSessionExpiry'

/** Top of the route tree: app-wide behaviour that must work on every page, signed in or not. */
export function RootLayout() {
  useSessionExpiry()
  return (
    <>
      <ConnectionBanner />
      <Outlet />
    </>
  )
}
