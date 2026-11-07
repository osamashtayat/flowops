import { useState } from 'react'
import { Outlet, useLocation } from 'react-router'
import { Dialog } from '@/components/ui/Dialog'
import { useRealtime } from '@/lib/realtime/useRealtime'
import { Sidebar } from './Sidebar'
import { TopBar } from './TopBar'

/**
 * Application frame (docs/DESIGN_SYSTEM.md §4): sand sidebar (fixed ≥ 1024 px, off-canvas drawer
 * below), top bar, ivory canvas with the page content (max 1200 px).
 */
export function AppShell() {
  const { pathname } = useLocation()
  useRealtime()
  // The drawer is open only on the page where it was opened, so any navigation (a link, the back
  // button) closes it without an effect.
  const [openedAt, setOpenedAt] = useState<string | null>(null)
  const drawerOpen = openedAt === pathname
  const setDrawerOpen = (open: boolean) => setOpenedAt(open ? pathname : null)

  return (
    <div className="flex min-h-dvh bg-canvas text-ink">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-sm focus:bg-surface focus:px-3 focus:py-2"
      >
        Skip to content
      </a>
      <aside className="sticky top-0 hidden h-dvh w-62 shrink-0 overflow-y-auto border-r border-border bg-sidebar lg:block">
        <div className="px-6 py-5 text-lg font-semibold">FlowOps</div>
        <Sidebar label="Main" />
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar onOpenMenu={() => setDrawerOpen(true)} />
        <main id="main" tabIndex={-1} className="min-w-0 flex-1 px-4 py-8 sm:px-8">
          <div className="mx-auto max-w-300">
            <Outlet />
          </div>
        </main>
      </div>
      <Dialog open={drawerOpen} onClose={() => setDrawerOpen(false)} title="FlowOps" placement="start">
        <Sidebar label="Menu" onNavigate={() => setDrawerOpen(false)} />
      </Dialog>
    </div>
  )
}
