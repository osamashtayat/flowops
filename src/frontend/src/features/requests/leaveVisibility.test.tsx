import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { LEAVE_TEXT_VISIBILITY_WARNING, withLeaveVisibilityWarning } from '@/lib/format/visibility'
import { setMockUser } from '@/mocks/state'
import { renderRoutes } from '@/test/renderRoutes'

/** Mock seeds: 1 is a purchase, 3 a leave (both from the employee); 9 is a leave waiting for Finance, 1 a purchase waiting for Finance. */
const PURCHASE = '/requests/5eed0000-0000-4000-8000-000000000001'
const LEAVE = '/requests/5eed0000-0000-4000-8000-000000000003'
const LEAVE_AT_FINANCE = '/requests/5eed0000-0000-4000-8000-000000000009'

const WARNING = LEAVE_TEXT_VISIBILITY_WARNING

describe('withLeaveVisibilityWarning', () => {
  it('appends the warning for leave and leaves purchases alone', () => {
    expect(withLeaveVisibilityWarning('Up to 5 characters.', 'Leave')).toBe(`Up to 5 characters. ${WARNING}`)
    expect(withLeaveVisibilityWarning(undefined, 'Leave')).toBe(WARNING)
    expect(withLeaveVisibilityWarning('Up to 5 characters.', 'EquipmentPurchase')).toBe('Up to 5 characters.')
    expect(withLeaveVisibilityWarning(undefined, 'EquipmentPurchase')).toBeUndefined()
  })

  it('says it the way the issue asks', () => {
    expect(WARNING).toBe("Visible to everyone who can see this request. Don't repeat the leave reason.")
  })
})

describe('comment box', () => {
  it('warns on a leave request', async () => {
    renderRoutes(routes, LEAVE)
    const box = await screen.findByLabelText('Add a comment')
    expect(box).toHaveAccessibleDescription(expect.stringContaining(WARNING))
    expect(box).toHaveAccessibleDescription(expect.stringContaining('Up to 2000 characters.'))
  })

  it('stays quiet on a purchase request', async () => {
    renderRoutes(routes, PURCHASE)
    const box = await screen.findByLabelText('Add a comment')
    expect(box).toHaveAccessibleDescription('Up to 2000 characters.')
  })
})

describe('decision dialog from the request page', () => {
  it.each([
    ['Approve', 'Comment'],
    ['Reject', 'Reason'],
  ])('warns on a leave request (%s)', async (button, field) => {
    setMockUser('finance')
    renderRoutes(routes, LEAVE_AT_FINANCE)
    await userEvent.click(await screen.findByRole('button', { name: button }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByLabelText(new RegExp(field))).toHaveAccessibleDescription(expect.stringContaining(WARNING))
  })

  it('does not warn on a purchase request', async () => {
    setMockUser('finance')
    renderRoutes(routes, PURCHASE)
    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByLabelText(/Reason/)).not.toHaveAccessibleDescription(expect.stringContaining(WARNING))
  })
})

describe('decision dialog from the inbox', () => {
  async function openFromInbox(titlePattern: RegExp, button: string) {
    setMockUser('finance')
    renderRoutes(routes, '/inbox')
    const grid = await screen.findByRole('table', { name: 'Open tasks' })
    const row = (await within(grid).findAllByText(titlePattern))[0]?.closest('tr') as HTMLElement
    await userEvent.click(within(row).getByRole('button', { name: button }))
    return screen.findByRole('dialog')
  }

  it('warns for a leave row', async () => {
    const dialog = await openFromInbox(/Annual leave/, 'Approve')
    expect(within(dialog).getByLabelText(/Comment/)).toHaveAccessibleDescription(expect.stringContaining(WARNING))
  })

  it('does not warn for a purchase row', async () => {
    const dialog = await openFromInbox(/Dell XPS 15 laptop/, 'Approve')
    expect(within(dialog).getByLabelText(/Comment/)).not.toHaveAccessibleDescription(expect.stringContaining(WARNING))
  })
})
