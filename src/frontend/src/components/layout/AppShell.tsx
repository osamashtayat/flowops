import type { ReactNode } from 'react'
import { Outlet } from 'react-router'

/**
 * Application frame: sand sidebar + ivory canvas.
 * FE-04 replaces the placeholder navigation with capability-driven items, the
 * < 1024 px drawer and the TopBar (see docs/UI_SPEC.md §3 and DESIGN_SYSTEM.md §4).
 */
export function AppShell({ navigation }: { navigation?: ReactNode }) {
  return (
    <div className="flex min-h-dvh bg-canvas text-ink">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:rounded-sm focus:bg-surface focus:px-3 focus:py-2"
      >
        Skip to content
      </a>
      <aside className="hidden w-62 shrink-0 border-r border-border bg-sidebar lg:block">
        <div className="px-5 py-5 text-lg font-semibold">FlowOps</div>
        <nav aria-label="Main" className="px-3">
          {navigation}
        </nav>
      </aside>
      <main id="main" className="min-w-0 flex-1 px-4 py-8 sm:px-8">
        <div className="mx-auto max-w-300">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
