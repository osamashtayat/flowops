import { useId, type ChangeEvent, type InputHTMLAttributes, type Ref } from 'react'
import { normalizeMoneyInput } from '@/lib/forms/money'
import { cn } from '@/lib/cn'
import { singleLineControlClasses } from './controlStyles'

export interface MoneyInputProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'inputMode' | 'value' | 'defaultValue' | 'onChange'> {
  ref?: Ref<HTMLInputElement>
  /** The amount as a string, e.g. "1250.500" — same shape as `Money.amount` in the API contract. */
  value: string
  onChange: (amount: string) => void
}

/**
 * Amount field for JOD: digits and one dot, at most 3 decimals. Keystrokes and pastes that would
 * break that are ignored; the value is never converted to a number. Controlled — in a form use
 * react-hook-form's `Controller` / `useController`.
 */
export function MoneyInput({ value, onChange, className, 'aria-describedby': describedBy, ...rest }: MoneyInputProps) {
  const currencyId = useId()

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    const next = normalizeMoneyInput(event.target.value)
    if (next !== null) onChange(next)
  }

  return (
    <div className="relative">
      <input
        type="text"
        inputMode="decimal"
        autoComplete="off"
        value={value}
        onChange={handleChange}
        aria-describedby={[describedBy, currencyId].filter(Boolean).join(' ')}
        className={cn(singleLineControlClasses, 'pr-14 text-right tabular-nums', className)}
        {...rest}
      />
      <span
        id={currencyId}
        className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-ink-muted"
      >
        JOD
      </span>
    </div>
  )
}
