import { describe, expect, it } from 'vitest'
import { leaveSchema, purchaseSchema } from './schemas'

const purchase = { itemName: 'Laptop', description: 'For work', estimatedCost: '1250.500', justification: 'Old one broke' }
const leave = { startDate: '2030-01-10', endDate: '2030-01-12', leaveType: 'Annual', reason: '' }

const firstMessage = (result: { success: boolean; error?: { issues: { message: string }[] } }) => result.error?.issues[0]?.message

describe('purchaseSchema', () => {
  it('accepts a complete purchase and keeps the cost a string', () => {
    const result = purchaseSchema.safeParse(purchase)
    expect(result.success).toBe(true)
    expect(result.data?.estimatedCost).toBe('1250.500')
  })

  it.each([
    ['0', 'greater than 0'],
    ['0.000', 'greater than 0'],
    ['1000000.001', 'at most 1,000,000'],
    ['', 'decimal'],
    ['12.3456', 'decimal'],
  ])('rejects the cost "%s"', (estimatedCost, message) => {
    expect(firstMessage(purchaseSchema.safeParse({ ...purchase, estimatedCost }))).toContain(message)
  })

  it('accepts exactly the maximum cost and the maximum lengths', () => {
    expect(purchaseSchema.safeParse({ ...purchase, estimatedCost: '1000000' }).success).toBe(true)
    expect(purchaseSchema.safeParse({ ...purchase, itemName: 'a'.repeat(150), description: 'a'.repeat(2000) }).success).toBe(true)
  })

  it('rejects blank and too-long text', () => {
    expect(purchaseSchema.safeParse({ ...purchase, itemName: '   ' }).success).toBe(false)
    expect(purchaseSchema.safeParse({ ...purchase, itemName: 'a'.repeat(151) }).success).toBe(false)
    expect(purchaseSchema.safeParse({ ...purchase, justification: 'a'.repeat(2001) }).success).toBe(false)
  })
})

describe('leaveSchema', () => {
  it('accepts a valid range and a missing reason', () => {
    expect(leaveSchema.safeParse(leave).success).toBe(true)
  })

  it('rejects an end before the start, on the end date', () => {
    const result = leaveSchema.safeParse({ ...leave, endDate: '2030-01-09' })
    expect(result.error?.issues[0]?.path).toEqual(['endDate'])
  })

  it('allows 90 calendar days and rejects 91', () => {
    expect(leaveSchema.safeParse({ ...leave, startDate: '2030-01-01', endDate: '2030-03-31' }).success).toBe(true)
    expect(firstMessage(leaveSchema.safeParse({ ...leave, startDate: '2030-01-01', endDate: '2030-04-01' }))).toContain('90')
  })

  it('rejects a start date in the past', () => {
    expect(leaveSchema.safeParse({ ...leave, startDate: '2000-01-01', endDate: '2000-01-02' }).error?.issues[0]?.path).toEqual(['startDate'])
  })

  it('rejects an unknown leave type and an over-long reason', () => {
    expect(leaveSchema.safeParse({ ...leave, leaveType: 'Holiday' }).success).toBe(false)
    expect(leaveSchema.safeParse({ ...leave, reason: 'a'.repeat(1001) }).success).toBe(false)
  })
})
