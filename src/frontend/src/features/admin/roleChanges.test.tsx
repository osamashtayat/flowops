import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { createMockUser, decideMockRoleChange, getMockUser, proposeMockRoleChange } from '@/mocks/adminStore'
import { server } from '@/mocks/server'
import { setMockUser } from '@/mocks/state'
import { MOCK_USERS } from '@/mocks/users'
import { renderRoutes } from '@/test/renderRoutes'
import type { UserDto } from '@/lib/api/types'

const heading = (name: string) => screen.findByRole('heading', { level: 1, name })
const problem = (status: number, code: string) => HttpResponse.json({ status, code, traceId: 't' }, { status })
const editPath = (key: 'admin' | 'employee') => `/admin/users/${MOCK_USERS[key].id}`

function asAdmin(path: string) {
  setMockUser('admin')
  return renderRoutes(routes, path)
}

/** A second active administrator, as the backend would have after "create user" with that role. */
function addSecondAdmin(): UserDto {
  const created = createMockUser(MOCK_USERS.admin.id, { email: 'admin2@flowops.test', displayName: 'Nadia Admin', initialPassword: 'a-long-password-1', roles: ['Administrator'] })
  return created as UserDto
}

const me = () => getMockUser(MOCK_USERS.admin.id) as UserDto

describe('Edit roles form (ADR-0012)', () => {
  it('does not offer to add Administrator to an existing user, but still offers it when creating', async () => {
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    expect(screen.getByLabelText('Administrator')).toBeDisabled()
    expect(screen.getByText('Only given when an account is created.')).toBeInTheDocument()
    expect(screen.getByLabelText('HR officer')).toBeEnabled()
  })

  it('offers Administrator when creating a user', async () => {
    asAdmin('/admin/users/new')
    await heading('New user')
    expect(screen.getByLabelText('Administrator')).toBeEnabled()
  })

  it('lets an administrator keep or remove Administrator on someone who holds it', async () => {
    const other = addSecondAdmin()
    asAdmin(`/admin/users/${other.id}`)
    await heading('Nadia Admin')
    expect(screen.getByLabelText('Administrator')).toBeEnabled()
    expect(screen.getByLabelText('Administrator')).toBeChecked()
  })

  it('shows "Waiting for another administrator" after the 202 on your own roles and lists the change', async () => {
    addSecondAdmin()
    asAdmin(editPath('admin'))
    await heading('Sami Qasem')
    await userEvent.click(screen.getByLabelText('Finance officer'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('Role change sent. Waiting for another administrator.')).toBeInTheDocument()
    await heading('Role changes')
    const grid = await screen.findByRole('table', { name: 'Role changes' })
    expect(await within(grid).findByText('Waiting for another administrator')).toBeInTheDocument()
    expect(within(grid).getByText('Administrator, Finance officer')).toBeInTheDocument()
    // The roles themselves did not change.
    expect(me().roles).toEqual(['Administrator'])
    // The proposer can cancel; they cannot approve or reject.
    expect(within(grid).getByRole('button', { name: 'Cancel change' })).toBeInTheDocument()
    expect(within(grid).queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it.each([
    ['role_change.no_reviewer', /no other administrator can approve/i],
    ['role_change.already_pending', /already have a role change waiting/i],
    ['user.admin_role_on_create_only', /only be given when a new account is created/i],
  ])('maps %s into the banner', async (code, message) => {
    server.use(http.put('/api/users/:id/roles', () => problem(code === 'role_change.already_pending' ? 409 : 422, code)))
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    await userEvent.click(screen.getByLabelText('HR officer'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText(message)).toBeInTheDocument()
  })

  it('refuses a self role change with no other administrator (mock mirrors the 422)', async () => {
    asAdmin(editPath('admin'))
    await heading('Sami Qasem')
    await userEvent.click(screen.getByLabelText('HR officer'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText(/no other administrator can approve/i)).toBeInTheDocument()
  })
})

describe('Role changes list', () => {
  function otherAdminProposal() {
    const other = addSecondAdmin()
    return proposeMockRoleChange(other, ['Employee', 'FinanceOfficer'])
  }

  it('shows another administrator’s pending change with approve and reject, and no cancel', async () => {
    otherAdminProposal()
    asAdmin('/admin/role-changes')
    await heading('Role changes')
    const grid = await screen.findByRole('table', { name: 'Role changes' })
    expect(await within(grid).findByText('Nadia Admin')).toBeInTheDocument()
    expect(within(grid).getByText('Employee, Finance officer')).toBeInTheDocument()
    expect(within(grid).getByText('Now: Administrator')).toBeInTheDocument()
    // A change that expires in the future must not read "just now" (the proposal is 72 h from expiry).
    expect(within(grid).getByText(/^Expires in [23] d$/)).toBeInTheDocument()
    expect(within(grid).getByRole('button', { name: 'Approve' })).toBeInTheDocument()
    expect(within(grid).getByRole('button', { name: 'Reject' })).toBeInTheDocument()
    expect(within(grid).queryByRole('button', { name: 'Cancel change' })).toBeNull()
  })

  it('renders only the actions the server lists, whatever the roles of the viewer', async () => {
    const change = { ...otherAdminProposal(), availableActions: ['approve' as const] }
    server.use(http.get('/api/admin/role-changes', () => HttpResponse.json({ items: [change], page: 1, pageSize: 20, totalCount: 1 })))
    asAdmin('/admin/role-changes')
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reject' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Cancel change' })).toBeNull()
  })

  it('approves after confirming; the roles change and the row becomes Approved', async () => {
    const change = otherAdminProposal()
    asAdmin('/admin/role-changes')
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Approve change' }))
    expect(await screen.findByText('Role change approved.')).toBeInTheDocument()
    await waitFor(() => expect(getMockUser(change.user.id)?.roles).toEqual(['Employee', 'FinanceOfficer']))
  })

  it('needs a reason to reject, then shows the decision', async () => {
    otherAdminProposal()
    asAdmin('/admin/role-changes')
    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject change' }))
    expect(await within(dialog).findByText('Enter a reason.')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText(/Reason/), 'Not needed')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject change' }))
    expect(await screen.findByText('Role change rejected.')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Status'), 'Rejected')
    expect(await screen.findByText('Reason: Not needed')).toBeInTheDocument()
    expect(screen.getByText(/Rejected by Sami Qasem/)).toBeInTheDocument()
  })

  it.each([
    [409, 'role_change.stale', /roles changed after the request was made/i],
    [409, 'role_change.not_pending', /no longer waiting for a decision/i],
    [409, 'concurrency.conflict', /changed by someone else/i],
    [422, 'role_change.self_approval', /decide on your own role change/i],
    [422, 'user.last_admin', /At least one active administrator/i],
  ])('shows the mapped message for %i %s and refetches the list', async (status, code, message) => {
    otherAdminProposal()
    let lists = 0
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'GET' && new URL(request.url).pathname === '/api/admin/role-changes') lists += 1
    })
    server.use(http.post('/api/admin/role-changes/:id/approve', () => problem(status, code)))
    asAdmin('/admin/role-changes')
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('dialog')
    const before = lists
    await userEvent.click(within(dialog).getByRole('button', { name: 'Approve change' }))
    expect(await within(dialog).findByText(message)).toBeInTheDocument()
    await waitFor(() => expect(lists).toBeGreaterThan(before))
    server.events.removeAllListeners()
  })

  it('still tells the user why when another administrator decided first and the row leaves the Pending list', async () => {
    const change = otherAdminProposal()
    asAdmin('/admin/role-changes')
    const approve = await screen.findByRole('button', { name: 'Approve' })
    decideMockRoleChange(MOCK_USERS.admin.id, change.id, 'approve', { version: change.version })
    await userEvent.click(approve)
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Approve change' }))
    expect((await screen.findAllByText(/no longer waiting for a decision|changed by someone else/i)).length).toBeGreaterThan(0)
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull())
  })

  it('lets the proposer cancel their own change', async () => {
    addSecondAdmin()
    proposeMockRoleChange(me(), ['Administrator', 'FinanceOfficer'])
    asAdmin('/admin/role-changes')
    expect(await screen.findByText('Waiting for another administrator')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Cancel change' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel change' }))
    expect(await screen.findByText('Role change cancelled.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('No role changes are waiting for approval.')).toBeInTheDocument())
  })

  it('has a calm empty state and a retry on failure', async () => {
    asAdmin('/admin/role-changes')
    expect(await screen.findByText('No role changes are waiting for approval.')).toBeInTheDocument()
  })

  it('shows a mapped error with Retry when the list fails', async () => {
    server.use(http.get('/api/admin/role-changes', () => problem(500, 'server.error')))
    asAdmin('/admin/role-changes')
    expect(await screen.findByText(/went wrong on our side/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('is closed to anyone without admin.manage', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/admin/role-changes')
    expect(await screen.findByText(/doesn't have permission to open this page/)).toBeInTheDocument()
  })
})
