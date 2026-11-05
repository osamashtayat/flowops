import { describe, expect, it } from 'vitest'
import { calendarDaysInclusive, formatDate, formatDateOnly, formatDateTime, todayInCompanyZone } from './date'
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

describe('company calendar day helpers', () => {
  it('reads "today" in Amman, not UTC', () => {
    // 22:30 UTC on 5 Nov is already 6 Nov in Amman (UTC+3 since 2022).
    expect(todayInCompanyZone(new Date('2026-11-05T22:30:00Z'))).toBe('2026-11-06')
    expect(todayInCompanyZone(new Date('2026-11-05T12:00:00Z'))).toBe('2026-11-05')
  })

  it('counts calendar days with both ends included', () => {
    expect(calendarDaysInclusive('2026-11-12', '2026-11-12')).toBe(1)
    expect(calendarDaysInclusive('2026-11-12', '2026-11-14')).toBe(3)
    expect(calendarDaysInclusive('2026-12-30', '2027-01-02')).toBe(4)
  })

  it('returns null for a missing, malformed or reversed range', () => {
    expect(calendarDaysInclusive('', '2026-11-14')).toBeNull()
    expect(calendarDaysInclusive('2026-11-14', '2026-11-12')).toBeNull()
  })
})
