import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import type { ActivityDto, RouteStepDto } from '@/lib/api/types'
import { getMockRequestFor } from '@/mocks/requestStore'
import { MOCK_STEPS } from '@/mocks/steps'
import { MOCK_USERS } from '@/mocks/users'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'

/** Seed 1: a purchase waiting at the second step. */
const PURCHASE = '5eed0000-0000-4000-8000-000000000001'
const PATH = `/requests/${PURCHASE}`

const problem = (status: number, code: string) => HttpResponse.json({ status, code, traceId: 't' }, { status })
const routeList = async () => within(await screen.findByRole('heading', { name: 'Route' }).then((h) => h.closest('div') as HTMLElement)).getByRole('list')
const rows = async () => within(await routeList()).getAllByRole('listitem')

/** The seeded purchase, with a route the server might send once conditions exist (P2-BE-05). */
function serveRoute(route: RouteStepDto[], activity: ActivityDto[] = []) {
  const request = getMockRequestFor(MOCK_USERS.employee, PURCHASE)
  if (!request) throw new Error('seed missing')
  server.use(
    http.get('/api/requests/:id', () => HttpResponse.json({ ...request, route })),
    http.get('/api/requests/:id/activity', () => HttpResponse.json(activity)),
  )
}

describe('route card', () => {
  it('lists the route in order, highlights the current step and marks the next one', async () => {
    renderRoutes(routes, PATH)
    const items = await rows()
    expect(items).toHaveLength(3)
    expect(items[0]).toHaveTextContent('Manager approval, done')
    expect(items[1]).toHaveTextContent('Finance approval, current step')
    expect(items[1]).toHaveAttribute('aria-current', 'step')
    expect(items[1]).toHaveTextContent('Waiting for Any of Finance officers')
    expect(items[2]).toHaveTextContent('Procurement fulfilment, planned')
    expect(items[2]).toHaveTextContent('Next')
    expect(items[2]).not.toHaveAttribute('aria-current')
  })

  it('shows a skipped step with its condition, in words as well as an icon', async () => {
    serveRoute(
      [
        { step: MOCK_STEPS.manager, kind: 'Approval', state: 'Done' },
        { step: MOCK_STEPS.finance, kind: 'Approval', state: 'Skipped' },
        { step: MOCK_STEPS.procurement, kind: 'Fulfillment', state: 'Current' },
      ],
      [
        {
          id: 'a1',
          type: 'StepSkipped',
          actor: null,
          stage: null,
          step: MOCK_STEPS.finance,
          body: 'Only if amount is greater than 1,000.000 JOD',
          occurredAt: '2026-11-01T09:00:00Z',
        },
      ],
    )
    renderRoutes(routes, PATH)
    const items = await rows()
    expect(items[1]).toHaveTextContent('Finance approval, skipped')
    expect(await within(items[1] as HTMLElement).findByText(/Only if amount is greater than 1,000.000 JOD/)).toBeInTheDocument()
    expect(items[1]).not.toHaveAttribute('aria-current')
    expect(items[2]).toHaveAttribute('aria-current', 'step')
  })

  it('still shows "Skipped" when the condition text is not available', async () => {
    serveRoute([
      { step: MOCK_STEPS.manager, kind: 'Approval', state: 'Done' },
      { step: MOCK_STEPS.finance, kind: 'Approval', state: 'Skipped' },
    ])
    renderRoutes(routes, PATH)
    expect((await rows())[1]).toHaveTextContent('Skipped')
  })

  it('writes a skipped step into the timeline with its condition', async () => {
    serveRoute([{ step: MOCK_STEPS.manager, kind: 'Approval', state: 'Done' }], [
      { id: 'a1', type: 'StepSkipped', actor: null, stage: null, step: MOCK_STEPS.finance, body: 'Only if amount is greater than 1,000.000 JOD', occurredAt: '2026-11-01T09:00:00Z' },
    ])
    renderRoutes(routes, PATH)
    expect(await screen.findByText('The Finance approval step was skipped')).toBeInTheDocument()
  })
})

describe('route preview on the new-request forms', () => {
  const fillCost = async (value: string) => userEvent.type(await screen.findByLabelText(/^Estimated cost/), value)

  it('is hidden until the cost is valid, then shows the server route', async () => {
    renderRoutes(routes, '/requests/new/purchase')
    await fillCost('0')
    expect(screen.queryByRole('heading', { name: 'What happens next' })).not.toBeInTheDocument()
    await userEvent.clear(screen.getByLabelText(/^Estimated cost/))
    await userEvent.type(screen.getByLabelText(/^Estimated cost/), '400')
    const preview = (await screen.findByRole('heading', { name: 'What happens next' })).closest('section') as HTMLElement
    const steps = await within(preview).findAllByRole('listitem')
    expect(steps.map((step) => step.textContent)).toEqual(['Manager approval', 'Finance approval', 'Procurement fulfilment'])
  })

  it('sends only the facts of the type and marks skipped steps', async () => {
    let body: unknown
    server.use(
      http.post('/api/requests/route-preview', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          steps: [
            { step: MOCK_STEPS.manager, kind: 'Approval', state: 'Planned' },
            { step: MOCK_STEPS.finance, kind: 'Approval', state: 'Skipped' },
            { step: MOCK_STEPS.procurement, kind: 'Fulfillment', state: 'Planned' },
          ],
        })
      }),
    )
    renderRoutes(routes, '/requests/new/purchase')
    await fillCost('400')
    expect(await screen.findByText(/Finance approval/)).toHaveTextContent('Finance approval (skipped)')
    expect(body).toEqual({ type: 'EquipmentPurchase', facts: { amount: { amount: '400', currency: 'JOD' } } })
  })

  it('says it is unavailable on an error and never blocks submitting', async () => {
    server.use(http.post('/api/requests/route-preview', () => problem(501, 'not_implemented')))
    renderRoutes(routes, '/requests/new/purchase')
    await fillCost('400')
    expect(await screen.findByText('Route preview unavailable')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Submit request' })).toBeEnabled()
  })

  it('shows the leave route once the dates are valid, sending days and type', async () => {
    let body: unknown
    server.use(
      http.post('/api/requests/route-preview', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ steps: [{ step: MOCK_STEPS.manager, kind: 'Approval', state: 'Planned' }, { step: MOCK_STEPS.hr, kind: 'Approval', state: 'Planned' }] })
      }),
    )
    setMockUser('employee')
    renderRoutes(routes, '/requests/new/leave')
    await userEvent.type(await screen.findByLabelText(/^Start date/), '2030-01-10')
    expect(screen.queryByRole('heading', { name: 'What happens next' })).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText(/^End date/), '2030-01-12')
    expect(await screen.findByText('HR confirmation')).toBeInTheDocument()
    expect(body).toEqual({ type: 'Leave', facts: { leaveType: 'Annual', days: 3 } })
  })
})
