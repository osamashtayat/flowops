import type { ReactNode } from 'react'

export interface CenteredPageProps {
  /** The page's h1. */
  title: string
  children: ReactNode
}

/**
 * Full-screen centred column for pages outside the app shell: sign in, change password,
 * 403, 404. Simple on purpose — explanation plus one way forward, no technical detail.
 */
export function CenteredPage({ title, children }: CenteredPageProps) {
  return (
    <main className="grid min-h-dvh place-items-center bg-canvas px-4 py-8 text-ink">
      <div className="w-full max-w-sm space-y-6">
        <div className="space-y-1 text-center">
          <p className="text-sm font-semibold text-ink-muted">FlowOps</p>
          <h1 className="text-xl font-semibold">{title}</h1>
        </div>
        {children}
      </div>
    </main>
  )
}
