import type { ReactNode } from 'react'
import { Inbox } from 'lucide-react'

export interface EmptyStateProps {
  /** One short sentence: "You have no requests yet." */
  message: string
  /** Optional next step, usually a Button or link. */
  action?: ReactNode
}

export function EmptyState({ message, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 px-5 py-10 text-center">
      <Inbox aria-hidden="true" className="size-6 text-ink-muted" />
      <p className="text-base text-ink-muted">{message}</p>
      {action}
    </div>
  )
}
