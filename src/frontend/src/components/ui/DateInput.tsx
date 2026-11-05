import type { InputHTMLAttributes, Ref } from 'react'
import { cn } from '@/lib/cn'
import { singleLineControlClasses } from './controlStyles'

export interface DateInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  ref?: Ref<HTMLInputElement>
}

/**
 * Native date picker. Its value is always `yyyy-MM-dd` (or ""), the same format as the API
 * (docs/API_CONTRACT.md conventions), so no conversion is needed. Use `min` / `max` for ranges,
 * e.g. end date `min={startDate}`.
 */
export function DateInput({ className, ...rest }: DateInputProps) {
  return <input type="date" className={cn(singleLineControlClasses, 'tabular-nums', className)} {...rest} />
}
