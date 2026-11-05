import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'

const heading = (name: string | RegExp) => screen.findByRole('heading', { level: 1, name })

function problem(status: number, code: string, errors?: Record<string, string[]>) {
  return HttpResponse.json({ status, code, traceId: 't', ...(errors ? { errors } : {}) }, { status })
}

async function fillPurchase(overrides: Partial<Record<'item' | 'description' | 'cost' | 'justification', string>> = {}) {
  const values = { item: 'Dell XPS 15', description: 'For the design team', cost: '1250.5', justification: 'Old laptop failed', ...overrides }
  if (values.item) await userEvent.type(await screen.findByLabelText(/^Item name/), values.item)
  if (values.description) await userEvent.type(screen.getByLabelText(/^Description/), values.description)
  if (values.cost) await userEvent.type(screen.getByLabelText(/^Estimated cost/), values.cost)
  if (values.justification) await userEvent.type(screen.getByLabelText(/^Business justification/), values.justification)
}

describe('new request: type picker', () => {
  it('offers both request types as links to their forms', async () => {
    renderRoutes(routes, '/requests/new')
    await heading('New request')
    expect(screen.getByRole('link', { name: /Equipment purchase/ })).toHaveAttribute('href', '/requests/new/purchase')
    expect(screen.getByRole('link', { name: /Leave/ })).toHaveAttribute('href', '/requests/new/leave')
  })

  it('sends someone without requests.submit to /forbidden', async () => {
    setMockUser('admin')
    const { router } = renderRoutes(routes, '/requests/new/purchase')
    await waitFor(() => expect(router.state.location.pathname).toBe('/forbidden'))
  })
})

describe('purchase form', () => {
  it('shows who is asking, from the session', async () => {
    renderRoutes(routes, '/requests/new/purchase')
    await heading('Equipment purchase')
    expect(screen.getByText('Requested by').nextElementSibling).toHaveTextContent('Layla Haddad')
    expect(screen.getByText('Department').nextElementSibling).toHaveTextContent('Engineering')
  })

  it('explains each missing field and focuses the first one, without calling the API', async () => {
    let called = false
    server.use(http.post('/api/requests/purchase', () => ((called = true), problem(500, 'server.error'))))
    renderRoutes(routes, '/requests/new/purchase')
    await userEvent.click(await screen.findByRole('button', { name: 'Submit request' }))

    expect(await screen.findByText('Enter the item name.')).toBeInTheDocument()
    expect(screen.getByText('Enter a description.')).toBeInTheDocument()
    expect(screen.getByText(/Enter an amount with up to 3 decimal/)).toBeInTheDocument()
    expect(screen.getByText('Enter a business justification.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Item name/)).toHaveFocus()
    expect(screen.getByLabelText(/^Item name/)).toHaveAttribute('aria-invalid', 'true')
    expect(called).toBe(false)
  })

  it('rejects a zero cost', async () => {
    renderRoutes(routes, '/requests/new/purchase')
    await fillPurchase({ cost: '0' })
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText('The cost must be greater than 0.')).toBeInTheDocument()
  })

  it('does not let a fourth decimal be typed', async () => {
    renderRoutes(routes, '/requests/new/purchase')
    const cost = await screen.findByLabelText(/^Estimated cost/)
    await userEvent.type(cost, '12.3456')
    expect(cost).toHaveValue('12.345')
  })

  it('submits only the contract fields, then opens the new request with a toast', async () => {
    let body: unknown
    server.use(
      http.post('/api/requests/purchase', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(
          { id: 'req-1', number: 'FO-2026-000901', type: 'EquipmentPurchase', title: 'Dell XPS 15', status: 'InReview', stage: 'ManagerApproval' },
          { status: 201 },
        )
      }),
    )
    const { router } = renderRoutes(routes, '/requests/new/purchase')
    await fillPurchase()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/requests/req-1'))
    expect(body).toEqual({
      itemName: 'Dell XPS 15',
      description: 'For the design team',
      justification: 'Old laptop failed',
      estimatedCost: { amount: '1250.5', currency: 'JOD' },
    })
    expect(await screen.findByText('Request FO-2026-000901 submitted. Your manager has been notified.')).toBeInTheDocument()
  })

  it('puts a server 400 on the matching field', async () => {
    server.use(http.post('/api/requests/purchase', () => problem(400, 'validation', { 'estimatedCost.amount': ['Amount is too large.'] })))
    renderRoutes(routes, '/requests/new/purchase')
    await fillPurchase()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText('Amount is too large.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Estimated cost/)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByText(/Something went wrong/)).not.toBeInTheDocument()
  })

  it('shows a 422 as a banner and keeps what was typed', async () => {
    server.use(http.post('/api/requests/purchase', () => problem(422, 'request.manager_not_assigned')))
    renderRoutes(routes, '/requests/new/purchase')
    await fillPurchase()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText(/You don't have a manager assigned yet/)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Item name/)).toHaveValue('Dell XPS 15')
  })

  it('blocks the form when the user has no manager', async () => {
    setMockUser('manager') // the mock department head has no manager
    renderRoutes(routes, '/requests/new/purchase')
    expect(await screen.findByText(/until an administrator assigns you/)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Item name/)).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Submit request' })).toBeDisabled()
  })

  it('Cancel goes back to the type picker', async () => {
    const { router } = renderRoutes(routes, '/requests/new/purchase')
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }))
    expect(router.state.location.pathname).toBe('/requests/new')
  })
})

describe('leave form', () => {
  const setDate = (label: RegExp, value: string) => userEvent.type(screen.getByLabelText(label), value)

  it('computes the calendar days and keeps the end date from going before the start', async () => {
    renderRoutes(routes, '/requests/new/leave')
    await heading('Leave')
    await setDate(/^Start date/, '2030-01-10')
    await setDate(/^End date/, '2030-01-12')
    expect(await screen.findByText('3 calendar days')).toBeInTheDocument()
    expect(screen.getByLabelText(/^End date/)).toHaveAttribute('min', '2030-01-10')
  })

  it('says the reason is private', async () => {
    renderRoutes(routes, '/requests/new/leave')
    expect(await screen.findByText(/Visible only to your manager and HR/)).toBeInTheDocument()
  })

  it('shows an error when the end is before the start', async () => {
    renderRoutes(routes, '/requests/new/leave')
    await heading('Leave')
    await setDate(/^Start date/, '2030-01-10')
    await setDate(/^End date/, '2030-01-09')
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText('The end date cannot be before the start date.')).toBeInTheDocument()
  })

  it('submits without a reason as null and opens the request', async () => {
    let body: unknown
    server.use(
      http.post('/api/requests/leave', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ id: 'req-2', number: 'FO-2026-000902' }, { status: 201 })
      }),
    )
    const { router } = renderRoutes(routes, '/requests/new/leave')
    await heading('Leave')
    await setDate(/^Start date/, '2030-01-10')
    await setDate(/^End date/, '2030-01-12')
    await userEvent.selectOptions(screen.getByLabelText(/^Leave type/), 'Sick')
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/requests/req-2'))
    expect(body).toEqual({ startDate: '2030-01-10', endDate: '2030-01-12', leaveType: 'Sick', reason: null })
  })

  it('maps a server 400 onto the date field', async () => {
    server.use(http.post('/api/requests/leave', () => problem(400, 'validation', { startDate: ['The start date cannot be in the past.'] })))
    renderRoutes(routes, '/requests/new/leave')
    await heading('Leave')
    await setDate(/^Start date/, '2030-01-10')
    await setDate(/^End date/, '2030-01-12')
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    const field = await screen.findByLabelText(/^Start date/)
    await waitFor(() => expect(field).toHaveAttribute('aria-invalid', 'true'))
    expect(within(field.parentElement as HTMLElement).getByText('The start date cannot be in the past.')).toBeInTheDocument()
  })
})
