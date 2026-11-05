import { describe, expect, it } from 'vitest'
import { formatDate, formatDateOnly, formatDateTime } from './date'
import { formatAmount, formatMoney } from './money'

describe('formatMoney', () => {
  it.each([
    ['1250.5', '1,250.500'],
    ['1250.500', '1,250.500'],
    ['0', '0.000'],
    ['7', '7.000'],
    ['1000000', '1,000,000.000'],
    ['12345678901234567890.123', '12,345,678,901,234,567,890.123'],
  ])('formats amount %s as %s without float rounding', (amount, expected) => {
    expect(formatAmount(amount)).toBe(expected)
  })

  it('appends the currency code', () => {
    expect(formatMoney({ amount: '1250.5', currency: 'JOD' })).toBe('1,250.500 JOD')
  })

  it('leaves malformed amounts untouched instead of throwing', () => {
    expect(formatAmount('abc')).toBe('abc')
  })
})

describe('date formatting (Asia/Amman)', () => {
  it('shows a UTC timestamp as a date in Amman (UTC+3)', () => {
    expect(formatDate('2026-11-05T09:00:00Z')).toBe('5 Nov 2026')
  })

  it('rolls over to the next day when Amman is already past midnight', () => {
    expect(formatDate('2026-11-05T22:30:00Z')).toBe('6 Nov 2026')
  })

  it('formats date and 24-hour time in Amman', () => {
    expect(formatDateTime('2026-11-05T11:05:00Z')).toBe('5 Nov 2026, 14:05')
    expect(formatDateTime('2026-11-05T21:00:00Z')).toBe('6 Nov 2026, 00:00')
  })

  it('writes September as "Sep"', () => {
    expect(formatDate('2026-09-15T09:00:00Z')).toBe('15 Sep 2026')
  })

  it('never shifts date-only values', () => {
    expect(formatDateOnly('2026-11-05')).toBe('5 Nov 2026')
    expect(formatDateOnly('2026-01-01')).toBe('1 Jan 2026')
  })

  it('returns unparseable input unchanged', () => {
    expect(formatDate('not a date')).toBe('not a date')
    expect(formatDateOnly('05/11/2026')).toBe('05/11/2026')
  })
})
