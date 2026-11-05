import { Check, CircleCheck, CircleX, Clock, type LucideIcon } from 'lucide-react'
import type { StatusIcon, StatusPresentation, StatusTone } from '@/lib/format/status'
import { cn } from '@/lib/cn'

const toneClasses: Record<StatusTone, string> = {
  info: 'bg-info-soft text-info',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  danger: 'bg-danger-soft text-danger',
  neutral: 'bg-neutral-soft text-neutral',
}

const icons: Record<StatusIcon, LucideIcon> = {
  clock: Clock,
  check: Check,
  'check-circle': CircleCheck,
  'x-circle': CircleX,
}

export interface StatusBadgeProps {
  /** Produced by lib/format/status.ts — components never decide labels or tones themselves. */
  status: StatusPresentation
  className?: string
}

/** Status pill: always label + icon + tone, never colour alone. */
export function StatusBadge({ status, className }: StatusBadgeProps) {
  const Icon = icons[status.icon]
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        toneClasses[status.tone],
        className,
      )}
    >
      <Icon aria-hidden="true" className="size-3" />
      {status.label}
    </span>
  )
}
