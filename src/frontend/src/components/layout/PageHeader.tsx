import type { ReactNode } from 'react'

export interface PageHeaderProps {
  /** The page's single h1. */
  title: string
  subtitle?: string
  /** Primary action(s), right-aligned (wraps below the title on narrow screens). One primary Button at most. */
  actions?: ReactNode
}

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
      <div className="min-w-0">
        <h1 className="text-xl font-semibold text-ink">{title}</h1>
        {subtitle && <p className="mt-1 max-w-[70ch] text-base text-ink-muted">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-3">{actions}</div>}
    </header>
  )
}
