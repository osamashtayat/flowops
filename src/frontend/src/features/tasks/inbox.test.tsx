import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { server } from '@/mocks/server'
import { setMockUser } from '@/mocks/state'
import { renderRoutes } from '@/test/renderRoutes'
import type { InboxItemDto } from '@/lib/api/types'

const heading = () => screen.findByRole('heading', { level: 1, name: 'Inbox' })
const openTable = () => screen.findByRole('table', { name: 'Open tasks' })
const bodyRows = (grid: HTMLElement) => within(grid).getAllByRole('row').slice(1)

const ITEM: InboxItemDto = {
  taskId: 'f1',
  kind: 'Fulfillment',
  stage: 'ProcurementFulfillment',
  createdAt: '2026-11-01T09:00:00Z',
  version: 'v1',
  status: 'Open',
  outcome: null,
  decidedAt: null,
  actions: ['fulfill'],
  request: {
    id: 'r1',
    number: 'FO-2026-000900',
    type: 'EquipmentPurchase',
    title: 'Standing desk',
    requester: { id: 'u1', displayName: 'Layla Haddad' },
    department: { id: 'd1', name: 'Engineering' },
    status: 'Approved',
    stage: 'ProcurementFulfillment',
    submittedAt: '2026-10-30T09:00:00Z',
    closedAt: null,
    keyDetail: '300.000 JOD',
  },
}

function financeInbox(path = '/inbox') {
  setMockUser('finance')
  return renderRoutes(routes, path)
}

describe('Inbox — Open', () => {
  it('lists the tasks I can decide with requester, detail, stage, age and actions', async () => {
    financeInbox()
    await heading()
    const grid = await openTable()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(3))
    expect(within(grid).getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
      'Request',
      'Requester',
      'Detail',
      'Stage',
      'Waiting',
      'Actions',
    ])
    const first = bodyRows(grid)[0] as HTMLElement
    expect(within(first).getByRole('link')).toHaveAttribute('href', expect.stringMatching(/^\/requests\/[\w-]+$/))
    expect(within(first).getByText('Layla Haddad')).toBeInTheDocument()
    expect(within(first).getByText('Engineering')).toBeInTheDocument()
    expect(within(first).getByText('Finance approval')).toBeInTheDocument()
    expect(within(first).getByRole('button', { name: 'Approve' })).toBeInTheDocument()
    expect(within(first).getByRole('button', { name: 'Reject' })).toBeInTheDocument()
  })

  it('offers only Fulfill on a fulfilment task', async () => {
    server.use(http.get('/api/tasks', () => HttpResponse.json({ items: [ITEM], page: 1, pageSize: 20, totalCount: 1 })))
    financeInbox()
    const grid = await openTable()
    expect(await within(grid).findByRole('button', { name: 'Fulfill' })).toBeInTheDocument()
    expect(within(grid).queryByRole('button', { name: 'Approve' })).toBeNull()
    expect(within(grid).queryByRole('button', { name: 'Reject' })).toBeNull()
  })

  it('shows exactly the actions the server lists, nothing derived from the task kind', async () => {
    server.use(
      http.get('/api/tasks', ({ request }) => {
        const only = { ...ITEM, actions: ['approve' as const] }
        const empty = new URL(request.url).searchParams.get('pageSize') === '1'
        return HttpResponse.json({ items: empty ? [] : [only], page: 1, pageSize: 20, totalCount: empty ? 0 : 1 })
      }),
    )
    financeInbox()
    const grid = await openTable()
    expect(await within(grid).findByRole('button', { name: 'Approve' })).toBeInTheDocument()
    expect(within(grid).queryByRole('button', { name: 'Reject' })).toBeNull()
  })

  it('approves from the row: confirm, announce, and the task leaves the list', async () => {
    financeInbox()
    const grid = await openTable()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(3))
    const number = within(bodyRows(grid)[0] as HTMLElement).getByText(/^FO-2026-/).textContent ?? ''
    await userEvent.click(within(bodyRows(grid)[0] as HTMLElement).getByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('heading', { name: `Approve ${number}?` })).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Approve request' }))
    expect(await screen.findByText(`${number} approved.`)).toBeInTheDocument()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(2))
    expect(within(grid).queryByText(number)).toBeNull()
  })

  it('requires a reason to reject', async () => {
    financeInbox()
    const grid = await openTable()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(3))
    await userEvent.click(within(bodyRows(grid)[0] as HTMLElement).getByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject request' }))
    expect(await within(dialog).findByText('Enter a reason.')).toBeInTheDocument()
    expect(bodyRows(grid)).toHaveLength(3)
  })

  it('says so and refreshes when someone else decided first (409)', async () => {
    server.use(http.post('/api/tasks/:taskId/approve', () => HttpResponse.json({ status: 409, code: 'task.already_decided', traceId: 't' }, { status: 409 })))
    financeInbox()
    const grid = await openTable()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(3))
    await userEvent.click(within(bodyRows(grid)[0] as HTMLElement).getByRole('button', { name: 'Approve' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Approve request' }))
    expect(await screen.findByText('Someone else already handled this task. The inbox has been refreshed.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('has a friendly empty state', async () => {
    server.use(http.get('/api/tasks', () => HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })))
    financeInbox()
    expect(await screen.findByText("You're all caught up.")).toBeInTheDocument()
  })

  it('shows a mapped error with a working Retry', async () => {
    let calls = 0
    server.use(
      http.get('/api/tasks', ({ request }) => {
        // The sidebar's Inbox badge asks for one row (`pageSize=1`); only the list may fail.
        if (new URL(request.url).searchParams.get('pageSize') === '1') return HttpResponse.json({ items: [], page: 1, pageSize: 1, totalCount: 0 })
        calls += 1
        return calls <= 3
          ? HttpResponse.json({ status: 500, code: 'server.error', traceId: 't' }, { status: 500 })
          : HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })
      }),
    )
    financeInbox()
    expect(await screen.findByText(/went wrong on our side/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByText("You're all caught up.")).toBeInTheDocument()
  })

  it('is closed to people without tasks.view', async () => {
    setMockUser('employee')
    renderRoutes(routes, '/inbox')
    expect(await screen.findByText(/doesn't have permission to open this page/)).toBeInTheDocument()
  })
})

describe('Inbox — Completed', () => {
  it('keeps the tab in the URL and lists what I decided with outcome and date', async () => {
    const { router } = financeInbox()
    const grid = await openTable()
    await waitFor(() => expect(bodyRows(grid)).toHaveLength(3))
    const number = within(bodyRows(grid)[0] as HTMLElement).getByText(/^FO-2026-/).textContent ?? ''
    await userEvent.click(within(bodyRows(grid)[0] as HTMLElement).getByRole('button', { name: 'Approve' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Approve request' }))
    await screen.findByText(`${number} approved.`)

    await userEvent.click(screen.getByRole('button', { name: 'Completed' }))
    expect(router.state.location.search).toBe('?status=Done')
    const done = await screen.findByRole('table', { name: 'Completed tasks' })
    expect(await within(done).findByText(number)).toBeInTheDocument()
    expect(within(done).getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
      'Request',
      'Requester',
      'Detail',
      'Stage',
      'Outcome',
      'Decided',
    ])
    expect(within(within(done).getByText(number).closest('tr') as HTMLElement).getByText('Approved')).toBeInTheDocument()
    expect(within(done).queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it('opens on Completed from a link, and is empty before any decision', async () => {
    server.use(http.get('/api/tasks', () => HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })))
    financeInbox('/inbox?status=Done')
    expect(await screen.findByText("You haven't decided any tasks yet.")).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Completed' })).toHaveAttribute('aria-current', 'page')
  })

  it('ignores an unknown status in the URL', async () => {
    financeInbox('/inbox?status=bogus')
    await openTable()
    expect(screen.getByRole('button', { name: 'Open' })).toHaveAttribute('aria-current', 'page')
  })
})
