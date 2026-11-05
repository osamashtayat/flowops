import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  requestStatusPresentation,
  taskStatusPresentation,
  type RequestStatus,
  type WorkTaskOutcome,
} from '@/lib/format/status'
import { StatusBadge } from './StatusBadge'

const requestCases: Array<[RequestStatus, string, string]> = [
  ['InReview', 'Pending review', 'text-warning'],
  ['Approved', 'Approved', 'text-info'],
  ['Completed', 'Completed', 'text-success'],
  ['Rejected', 'Rejected', 'text-danger'],
]

describe('StatusBadge', () => {
  it.each(requestCases)('renders request status %s as "%s" with tone %s', (status, label, toneClass) => {
    render(<StatusBadge status={requestStatusPresentation(status)} />)
    const badge = screen.getByText(label)
    expect(badge).toHaveClass(toneClass)
    // Status is never conveyed by colour alone: an icon accompanies the label.
    expect(badge.querySelector('svg[aria-hidden="true"]')).not.toBeNull()
  })

  const taskCases: Array<[WorkTaskOutcome | null, string]> = [
    [null, 'Awaiting action'],
    ['Approved', 'Approved'],
    ['Fulfilled', 'Fulfilled'],
    ['Rejected', 'Rejected'],
  ]

  it.each(taskCases)('maps task outcome %s to "%s"', (outcome, label) => {
    expect(taskStatusPresentation(outcome).label).toBe(label)
  })
})
