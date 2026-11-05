import { cn } from '@/lib/cn'

/**
 * Placeholder block. Size it to match the content it stands in for (`h-4 w-40`).
 * Decorative, so hidden from assistive tech — announce loading with a status message instead.
 * Motion is switched off globally by `prefers-reduced-motion` in theme.css.
 */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-sm bg-surface-muted', className)} />
}
