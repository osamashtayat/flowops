import { describe, expect, it } from 'vitest'
import { isCompleteMoneyAmount, moneyAmountSchema, normalizeMoneyInput } from './money'

describe('normalizeMoneyInput', () => {
  it.each([
    ['', ''],
    ['0', '0'],
    ['12.', '12.'],
    ['.5', '.5'],
    ['1250.500', '1250.500'],
    ['  42 ', '42'],
    ['1,234', '1234'],
    ['1,234,567.89', '1234567.89'],
  ])('keeps %j as %j', (raw, kept) => expect(normalizeMoneyInput(raw)).toBe(kept))

  it.each(['1.2345', '1.2.3', '-1', 'abc', '1,5', '12,34', '1,23,456', '1e3', '1 000'])('rejects %j', (raw) =>
    expect(normalizeMoneyInput(raw)).toBeNull(),
  )
})

describe('moneyAmountSchema', () => {
  it.each(['1', '1.5', '1250.500', '0.001'])('accepts %s', (v) => expect(moneyAmountSchema.safeParse(v).success).toBe(true))

  it.each(['', '.', '12.', '.5', '1.2345', 'x'])('rejects %j', (v) => {
    expect(isCompleteMoneyAmount(v)).toBe(false)
    expect(moneyAmountSchema.safeParse(v).success).toBe(false)
  })
})
