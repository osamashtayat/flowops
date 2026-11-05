import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

export interface CardProps {
  /** Optional header, rendered as a heading (not a landmark: a page of cards would flood the landmark list). */
  title?: string
  /** Heading level for `title`; pick the one that fits the page outline (the page's h1 is PageHeader). */
  headingLevel?: 2 | 3
  /** Right-aligned header content, e.g. a Button. */
  actions?: ReactNode
  className?: string
  children: ReactNode
}

export function Card({ title, headingLevel = 2, actions, className, children }: CardProps) {
  const Heading = headingLevel === 2 ? 'h2' : 'h3'
  return (
    <div className={cn('rounded-md border border-border bg-surface shadow-card', className)}>
      {title && (
        <header className="flex items-center justify-between gap-3 border-b border-border px-5 py-4">
          <Heading className="text-lg font-semibold text-ink">
            {title}
          </Heading>
          {actions}
        </header>
      )}
      <div className="p-5">{children}</div>
    </div>
  )
}
