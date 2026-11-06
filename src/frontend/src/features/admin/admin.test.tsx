import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { server } from '@/mocks/server'
import { setMockUser } from '@/mocks/state'
import { MOCK_USERS } from '@/mocks/users'
import { renderRoutes } from '@/test/renderRoutes'

const heading = (name: string) => screen.findByRole('heading', { level: 1, name })
const problem = (status: number, code: string) => HttpResponse.json({ status, code, traceId: 't' }, { status })

function asAdmin(path: string) {
  setMockUser('admin')
  return renderRoutes(routes, path)
}

describe('Users list', () => {
  it('lists users with department, manager, role chips and status', async () => {
    asAdmin('/admin/users')
    await heading('Users')
    const grid = await screen.findByRole('table', { name: 'Users' })
    expect(within(grid).getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Name', 'Email', 'Department', 'Manager', 'Roles', 'Status'])
    const row = (await within(grid).findByRole('link', { name: 'Layla Haddad' })).closest('tr')
    expect(row).not.toBeNull()
    const cells = within(row as HTMLElement)
    expect(cells.getByText('Engineering')).toBeInTheDocument()
    expect(cells.getByText('Omar Khalil')).toBeInTheDocument()
    expect(cells.getByText('Employee')).toBeInTheDocument()
    expect(cells.getByText('Active')).toBeInTheDocument()
  })

  it('hides inactive users until asked, and sends the filters to the API', async () => {
    const seen: URL[] = []
    server.use(
      http.get('/api/users', ({ request }) => {
        seen.push(new URL(request.url))
        return HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })
      }),
    )
    asAdmin('/admin/users?role=HrOfficer&inactive=true&search=ann')
    expect(await screen.findByText('No users match these filters.')).toBeInTheDocument()
    const last = seen.at(-1)
    expect(last?.searchParams.get('role')).toBe('HrOfficer')
    expect(last?.searchParams.get('includeInactive')).toBe('true')
    expect(last?.searchParams.get('search')).toBe('ann')
  })

  it('pages through the list', async () => {
    asAdmin('/admin/users')
    const grid = await screen.findByRole('table', { name: 'Users' })
    await waitFor(() => expect(within(grid).getAllByRole('row').length).toBe(21))
    await userEvent.click(screen.getByRole('button', { name: /next/i }))
    await waitFor(() => expect(screen.getByText(/Showing 21–/)).toBeInTheDocument())
  })

  it('shows a mapped error with Retry when the list fails', async () => {
    server.use(http.get('/api/users', () => problem(500, 'server.error')))
    asAdmin('/admin/users')
    expect(await screen.findByText(/went wrong on our side/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('is closed to anyone without admin.manage', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/admin/users')
    expect(await screen.findByText(/doesn't have permission to open this page/)).toBeInTheDocument()
  })
})

describe('New user', () => {
  it('checks the fields before calling the API', async () => {
    asAdmin('/admin/users/new')
    await heading('New user')
    await userEvent.click(screen.getByRole('button', { name: 'Create user' }))
    expect(await screen.findByText('Enter a display name.')).toBeInTheDocument()
    expect(screen.getByText('Enter an email address.')).toBeInTheDocument()
    expect(screen.getByText('Use at least 12 characters.')).toBeInTheDocument()
  })

  it('creates the user, announces it and returns to the list', async () => {
    asAdmin('/admin/users/new')
    await heading('New user')
    await userEvent.type(screen.getByLabelText(/Display name/), 'Huda Saleh')
    await userEvent.type(screen.getByLabelText(/^Email/), 'huda@flowops.test')
    await userEvent.type(screen.getByLabelText(/Initial password/), 'a-long-initial-pass')
    await userEvent.selectOptions(screen.getByLabelText('Department'), 'Finance')
    await userEvent.click(screen.getByLabelText('Finance officer'))
    await userEvent.click(screen.getByRole('button', { name: 'Create user' }))
    expect(await screen.findByText('Huda Saleh was created.')).toBeInTheDocument()
    await heading('Users')
  })

  it('shows the duplicate-email message from the server', async () => {
    asAdmin('/admin/users/new')
    await heading('New user')
    await userEvent.type(screen.getByLabelText(/Display name/), 'Copy Cat')
    await userEvent.type(screen.getByLabelText(/^Email/), MOCK_USERS.employee.email)
    await userEvent.type(screen.getByLabelText(/Initial password/), 'a-long-initial-pass')
    await userEvent.click(screen.getByRole('button', { name: 'Create user' }))
    expect(await screen.findByText('A user with this email already exists.')).toBeInTheDocument()
  })

  it("never offers the signed-in administrator as a manager", async () => {
    asAdmin('/admin/users/new')
    await heading('New user')
    const manager = await screen.findByLabelText('Manager')
    await waitFor(() => expect(within(manager).getAllByRole('option').length).toBeGreaterThan(1))
    expect(within(manager).queryByText(new RegExp(MOCK_USERS.admin.displayName))).toBeNull()
    expect(within(manager).getByText(/Omar Khalil/)).toBeInTheDocument()
  })
})

describe('Edit user', () => {
  const editPath = (key: keyof typeof MOCK_USERS) => `/admin/users/${MOCK_USERS[key].id}`

  it('saves a changed name and added role in one go', async () => {
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    const name = screen.getByLabelText(/Display name/)
    await userEvent.clear(name)
    await userEvent.type(name, 'Layla H.')
    await userEvent.click(screen.getByLabelText('HR officer'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('Layla H. was updated.')).toBeInTheDocument()
  })

  it("locks the administrator's own roles, manager and active switch and explains why", async () => {
    asAdmin(editPath('admin'))
    await heading('Sami Qasem')
    expect(screen.getByText(/own account. Another administrator has to change/)).toBeInTheDocument()
    for (const label of ['Administrator', 'Employee', 'Finance officer', 'HR officer', 'Procurement officer', 'Active']) {
      expect(screen.getByLabelText(label)).toBeDisabled()
    }
    expect(screen.getByLabelText('Manager')).toBeDisabled()
    expect(screen.getByLabelText(/Display name/)).toBeEnabled()
    expect(screen.getByLabelText('Department')).toBeEnabled()
  })

  it("lets the administrator rename themselves without sending the locked fields' form values", async () => {
    let body: unknown
    server.use(
      http.put('/api/users/:id', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ ...MOCK_USERS.admin, isActive: true, mustChangePassword: false, directReportCount: 0, createdAt: '2026-01-01T00:00:00Z', displayName: 'Sami Q.' })
      }),
    )
    asAdmin(editPath('admin'))
    await heading('Sami Qasem')
    const name = screen.getByLabelText(/Display name/)
    await userEvent.clear(name)
    await userEvent.type(name, 'Sami Q.')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    await screen.findByText('Sami Q. was updated.')
    expect(body).toEqual({ displayName: 'Sami Q.', departmentId: null, managerId: null, isActive: true })
  })

  it("maps the server's 422 for a self change into the banner", async () => {
    server.use(http.put('/api/users/:id', () => problem(422, 'user.self_administration')))
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('Another administrator has to make this change.')).toBeInTheDocument()
  })

  it('says why the last administrator cannot be removed', async () => {
    server.use(http.put('/api/users/:id', () => problem(422, 'user.last_admin')))
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('At least one active administrator is required.')).toBeInTheDocument()
  })

  it('shows a not-found message for an unknown user', async () => {
    asAdmin('/admin/users/00000000-0000-4000-8000-00000000dead')
    expect(await screen.findByText(/doesn't exist or you don't have access/)).toBeInTheDocument()
  })

  it('resets a password after validation and confirms it', async () => {
    asAdmin(editPath('employee'))
    await heading('Layla Haddad')
    await userEvent.click(screen.getByRole('button', { name: 'Reset password…' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reset password' }))
    expect(await within(dialog).findByText('Use at least 12 characters.')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText(/New temporary password/), 'another-long-pass')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reset password' }))
    expect(await screen.findByText('Password reset for Layla Haddad.')).toBeInTheDocument()
  })
})

describe('Departments', () => {
  it('lists departments with members and status, including inactive ones', async () => {
    asAdmin('/admin/departments')
    await heading('Departments')
    const grid = await screen.findByRole('table', { name: 'Departments' })
    expect(await within(grid).findByText('Engineering')).toBeInTheDocument()
    expect(within(grid).getByText('Legacy Projects')).toBeInTheDocument()
    expect(within(grid).getByText('Inactive')).toBeInTheDocument()
  })

  it('creates a department, and says when the name or code is taken', async () => {
    asAdmin('/admin/departments')
    await heading('Departments')
    await userEvent.click(screen.getByRole('button', { name: 'New department' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create department' }))
    expect(await within(dialog).findByText('Enter a name.')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText(/Name/), 'Engineering')
    await userEvent.type(within(dialog).getByLabelText(/Code/), 'x1')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create department' }))
    expect(await within(dialog).findByText('A department with this name or code already exists.')).toBeInTheDocument()

    await userEvent.clear(within(dialog).getByLabelText(/Name/))
    await userEvent.type(within(dialog).getByLabelText(/Name/), 'Operations')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create department' }))
    expect(await screen.findByText('Operations was created.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(await screen.findByText('X1')).toBeInTheDocument()
  })

  it('edits and deactivates a department', async () => {
    asAdmin('/admin/departments')
    await heading('Departments')
    await userEvent.click(await screen.findByRole('button', { name: 'Edit Finance' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByLabelText('Active'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByText('Finance was updated.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getAllByText('Inactive').length).toBe(2))
  })
})

describe('Stalled tasks', () => {
  it('lists stalled tasks with the reason and what to do', async () => {
    asAdmin('/admin/stalled-tasks')
    await heading('Stalled tasks')
    const grid = await screen.findByRole('table', { name: 'Stalled tasks' })
    expect(await within(grid).findByText('FO-2026-000201')).toBeInTheDocument()
    expect(within(grid).getByText('Add an active Finance officer.')).toBeInTheDocument()
    expect(within(grid).getByText('Omar Khalil')).toBeInTheDocument()
    expect(within(grid).getByText('Reactivate the assignee, or give the requester a new manager.')).toBeInTheDocument()
    expect(within(grid).queryByRole('link')).toBeNull()
  })

  it('has a calm empty state', async () => {
    server.use(http.get('/api/admin/stalled-tasks', () => HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })))
    asAdmin('/admin/stalled-tasks')
    expect(await screen.findByText(/No stalled tasks/)).toBeInTheDocument()
  })
})
