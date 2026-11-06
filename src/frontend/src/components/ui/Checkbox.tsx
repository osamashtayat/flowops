import { useId, type InputHTMLAttributes, type ReactNode, type Ref } from 'react'
import { cn } from '@/lib/cn'

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'children'> {
  ref?: Ref<HTMLInputElement>
  label: string
  /** Extra line under the label, e.g. why the box is disabled. */
  hint?: ReactNode
}

/** Native checkbox with its label (DESIGN_SYSTEM §5 "Inputs"); the whole label is the click target. */
export function Checkbox({ label, hint, className, id, ...rest }: CheckboxProps) {
  const generated = useId()
  const inputId = id ?? generated
  const hintId = `${inputId}-hint`
  return (
    <div className={cn('flex items-start gap-3', className)}>
      <input
        id={inputId}
        type="checkbox"
        aria-describedby={hint ? hintId : undefined}
        className="mt-0.5 size-5 shrink-0 accent-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:cursor-not-allowed disabled:opacity-50"
        {...rest}
      />
      <div className="min-w-0">
        <label htmlFor={inputId} className="text-base text-ink">
          {label}
        </label>
        {hint && (
          <p id={hintId} className="text-xs text-ink-muted">
            {hint}
          </p>
        )}
      </div>
    </div>
  )
}
