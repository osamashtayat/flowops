import { useId, type ReactNode } from 'react'
import { cn } from '@/lib/cn'

/** Accessibility attributes FormField hands to the control it wraps. Spread them on the control. */
export interface FormFieldControlProps {
  id: string
  required: boolean | undefined
  'aria-describedby': string | undefined
  'aria-invalid': true | undefined
}

export interface FormFieldProps {
  label: string
  required?: boolean
  helperText?: string
  /** Message to show; its presence marks the control invalid. */
  error?: string
  className?: string
  /** Render prop so the control gets its id, `required`, `aria-describedby` and `aria-invalid`. */
  children: (control: FormFieldControlProps) => ReactNode
}

/**
 * Label + control + helper + error as one vertical stack (DESIGN_SYSTEM §5 "FormField").
 * The visible "*" is decoration (`aria-hidden`); screen readers get the native `required`.
 *
 *   <FormField label="Item name" required error={fieldError(errors, 'itemName')}>
 *     {(control) => <Input {...control} {...register('itemName')} />}
 *   </FormField>
 */
export function FormField({ label, required, helperText, error, className, children }: FormFieldProps) {
  const id = useId()
  const helperId = `${id}-helper`
  const errorId = `${id}-error`
  const describedBy = [helperText ? helperId : null, error ? errorId : null].filter(Boolean).join(' ')

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-medium text-ink">
        {label}
        {required && (
          <span aria-hidden="true" className="text-danger">
            {' '}
            *
          </span>
        )}
      </label>
      {children({
        id,
        required: required || undefined,
        'aria-describedby': describedBy || undefined,
        'aria-invalid': error ? true : undefined,
      })}
      {helperText && (
        <p id={helperId} className="text-xs text-ink-muted">
          {helperText}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
