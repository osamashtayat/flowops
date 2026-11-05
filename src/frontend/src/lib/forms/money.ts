import { z } from 'zod'

/**
 * Money amounts travel as strings (docs/API_CONTRACT.md §1: `{ amount: "1250.500", currency: "JOD" }`)
 * so JavaScript floats never touch them. JOD has three minor-unit decimals (DOMAIN_MODEL §Money).
 * This is the single home of the "what is a well-formed amount" knowledge; range limits
 * (e.g. > 0 and ≤ 1,000,000 for a purchase) belong to the feature schema that uses it.
 */
export const MONEY_MAX_DECIMALS = 3

const TYPING = new RegExp(`^\\d*(\\.\\d{0,${MONEY_MAX_DECIMALS}})?$`)
const GROUPED = new RegExp(`^\\d{1,3}(,\\d{3})+(\\.\\d{0,${MONEY_MAX_DECIMALS}})?$`)
// Same pattern as MoneyDto.amount in contracts/openapi.json: no leading zeros, at most 7 integer digits.
const COMPLETE = new RegExp(`^(0|[1-9]\\d{0,6})(\\.\\d{1,${MONEY_MAX_DECIMALS}})?$`)

/**
 * Turns raw keyboard/paste text into the string to keep, or `null` if it must be rejected.
 * Accepts partial input while typing ("", "12.", ".5") and strictly grouped thousands ("1,234.5").
 * A comma anywhere else is rejected rather than guessed at: "1,5" may be a decimal comma, and
 * silently turning it into 15 would change the amount.
 */
export function normalizeMoneyInput(raw: string): string | null {
  const text = raw.trim()
  if (TYPING.test(text)) return text
  if (GROUPED.test(text)) return text.replaceAll(',', '')
  return null
}

/** True for a finished amount: digits with 1–3 optional decimals ("12", "12.5", "12.500"). */
export function isCompleteMoneyAmount(amount: string): boolean {
  return COMPLETE.test(amount)
}

export const moneyAmountSchema = z
  .string()
  .trim()
  .refine(isCompleteMoneyAmount, { message: `Enter an amount with up to ${MONEY_MAX_DECIMALS} decimal places.` })
