import type { Ref, SelectHTMLAttributes } from 'react'
import { ChevronDown } from 'lucide-react'
import { cn } from '@/lib/cn'
import { singleLineControlClasses } from './controlStyles'

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  ref?: Ref<HTMLSelectElement>
  /** Adds a leading empty option with this text (value ""), for "nothing chosen yet". */
  placeholder?: string
}

/** Native select (best keyboard and screen-reader behaviour) with the design-system chevron. */
export function Select({ className, placeholder, children, ...rest }: SelectProps) {
  return (
    <div className="relative">
      <select className={cn(singleLineControlClasses, 'appearance-none pr-9', className)} {...rest}>
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {children}
      </select>
      <ChevronDown
        aria-hidden="true"
        className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-ink-muted"
      />
    </div>
  )
}
