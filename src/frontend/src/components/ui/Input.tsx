import type { InputHTMLAttributes, Ref } from 'react'
import { cn } from '@/lib/cn'
import { singleLineControlClasses } from './controlStyles'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  ref?: Ref<HTMLInputElement>
}

/** Text input. Get id / aria-describedby / aria-invalid / required from `FormField`. */
export function Input({ className, type = 'text', ...rest }: InputProps) {
  return <input type={type} className={cn(singleLineControlClasses, className)} {...rest} />
}
