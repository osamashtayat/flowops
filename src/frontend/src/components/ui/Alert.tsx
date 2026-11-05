import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

export type AlertTone = 'danger' | 'warning' | 'info'

const TONE_CLASSES: Record<AlertTone, string> = {
  danger: 'bg-danger-soft text-danger',
  warning: 'bg-warning-soft text-warning',
  info: 'bg-info-soft text-info',
}

export interface AlertProps {
  tone: AlertTone
  children: ReactNode
  className?: string
}

/**
 * A short message inside a page or form (not a toast). Errors use `role="alert"` so they are
 * announced when they appear; warnings and info are plain text in reading order.
 */
export function Alert({ tone, children, className }: AlertProps) {
  return (
    <p role={tone === 'danger' ? 'alert' : undefined} className={cn('rounded-sm px-3 py-2 text-sm', TONE_CLASSES[tone], className)}>
      {children}
    </p>
  )
}
