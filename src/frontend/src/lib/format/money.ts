import type { MoneyDto } from '@/lib/api/types'

/**
 * Money display. Amounts are strings (docs/API_CONTRACT.md §1) and are formatted by string
 * manipulation only, so no float ever rounds them.
 */
export type Money = MoneyDto

const AMOUNT = /^(\d+)(?:\.(\d+))?$/

/** "1250.5" → "1,250.500". Anything that is not a plain decimal string is returned unchanged. */
export function formatAmount(amount: string): string {
  const match = AMOUNT.exec(amount)
  if (!match) return amount
  const [, whole = '0', fraction = ''] = match
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  return `${grouped}.${fraction.padEnd(3, '0')}`
}

/** "1,250.500 JOD" — three decimals, the currency code after the number. */
export function formatMoney(money: Money): string {
  return `${formatAmount(money.amount)} ${money.currency}`
}
