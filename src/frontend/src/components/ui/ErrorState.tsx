import { TriangleAlert } from 'lucide-react'
import { Button } from './Button'

export interface ErrorStateProps {
  /** What happened and what to do — from `toUserMessage(error)` once FE-01 lands. */
  message: string
  onRetry?: () => void
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 px-5 py-10 text-center">
      <TriangleAlert aria-hidden="true" className="size-6 text-danger" />
      <p className="text-base text-ink">{message}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          Retry
        </Button>
      )}
    </div>
  )
}
