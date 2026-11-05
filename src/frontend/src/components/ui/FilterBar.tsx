import type { ReactNode } from 'react'
import { Button } from './Button'

export interface FilterBarProps {
  /** Accessible name of the search region. */
  label?: string
  /** `FormField`s holding Select / Input controls. Bind their values to the URL with `useUrlState`. */
  children: ReactNode
  hasActiveFilters?: boolean
  onClear?: () => void
}

export function FilterBar({ label = 'Filters', children, hasActiveFilters = false, onClear }: FilterBarProps) {
  return (
    <search aria-label={label} className="flex flex-wrap items-end gap-3">
      {children}
      {hasActiveFilters && onClear && (
        <Button variant="ghost" onClick={onClear}>
          Clear filters
        </Button>
      )}
    </search>
  )
}
