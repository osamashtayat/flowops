import type { Ref, TextareaHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { controlClasses } from './controlStyles'

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  ref?: Ref<HTMLTextAreaElement>
}

export function Textarea({ className, rows = 4, ...rest }: TextareaProps) {
  return <textarea rows={rows} className={cn(controlClasses, 'min-h-24 resize-y py-2', className)} {...rest} />
}
