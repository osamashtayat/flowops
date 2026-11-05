import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'

const heading = (name: string) => screen.findByRole('heading', { level: 1, name })
const empty = {
  myRequests: { inReview: 0, approved: 0, completed: 0, rejected: 0, recent: [] },
  myTasks: null,
  recentActivity: [],
  admin: null,
}

describe('dashboard', () => {
  it('shows an employee their request counts, recent requests and activity — and no queue or admin cards', async () => {
    renderRoutes(routes, '/')
    await heading('Dashboard')
    const tiles = await screen.findByRole('list', { name: 'My requests by status' })
    // 23 seeded requests cycle InReview, Approved, Completed, Rejected → 6, 6, 6, 5.
    expect(within(tiles).getByRole('link', { name: /Pending review\s*6/ })).toHaveAttribute('href', '/requests?status=InReview')
    expect(within(tiles).getByRole('link', { name: /Rejected\s*5/ })).toHaveAttribute('href', '/requests?status=Rejected')

    const recent = screen.getByRole('table', { name: 'Recent requests' })
    expect(within(recent).getAllByRole('row')).toHaveLength(6) // header + 5
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute('href', '/requests')

    expect(screen.getByRole('heading', { name: 'Recent activity' })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /^FO-2026-\d{6}$/ }).length).toBeGreaterThan(0)

    expect(screen.queryByRole('heading', { name: 'Your tasks' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Administration' })).not.toBeInTheDocument()
  })

  it('opens the request list for the status of a tile', async () => {
    const { router } = renderRoutes(routes, '/')
    await userEvent.click(await screen.findByRole('link', { name: /Approved\s*6/ }))
    await waitFor(() => expect(router.state.location.pathname + router.state.location.search).toBe('/requests?status=Approved'))
  })

  it('tells someone who works a queue how many tasks wait, and links to the inbox', async () => {
    setMockUser('finance')
    renderRoutes(routes, '/')
    const card = (await screen.findByRole('heading', { name: 'Your tasks' })).closest('div') as HTMLElement
    expect(await within(card.parentElement as HTMLElement).findByText(/tasks are/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open inbox' })).toHaveAttribute('href', '/inbox')
  })

  it('says nothing is waiting when the queue is empty', async () => {
    setMockUser('manager')
    server.use(
      http.get('/api/dashboard', () =>
        HttpResponse.json({ ...empty, myTasks: { open: 0, oldestOpenCreatedAt: null, decidedLast7Days: 4 } }),
      ),
    )
    renderRoutes(routes, '/')
    expect(await screen.findByText('Nothing is waiting for you.')).toBeInTheDocument()
    expect(screen.getByText(/in the last 7 days/)).toBeInTheDocument()
  })

  it('follows the server: a null section is not shown even for a manager', async () => {
    setMockUser('manager')
    server.use(http.get('/api/dashboard', () => HttpResponse.json(empty)))
    renderRoutes(routes, '/')
    await heading('Dashboard')
    await screen.findByText('You have no requests yet.')
    expect(screen.queryByRole('heading', { name: 'Your tasks' })).not.toBeInTheDocument()
  })

  it('shows an administrator the administration card, with stalled tasks as a warning, and no personal request tiles', async () => {
    setMockUser('admin')
    renderRoutes(routes, '/')
    await heading('Dashboard')
    const card = within(await screen.findByRole('main'))
    const stalled = await card.findByRole('link', { name: /Stalled tasks/ })
    expect(stalled).toHaveAttribute('href', '/admin/stalled-tasks')
    expect(within(stalled).getByText('Needs attention')).toBeInTheDocument()
    expect(card.getByRole('link', { name: /Active users/ })).toHaveAttribute('href', '/admin/users')
    expect(screen.queryByRole('list', { name: 'My requests by status' })).not.toBeInTheDocument()
  })

  it('welcomes an employee with no requests and offers New request', async () => {
    server.use(http.get('/api/dashboard', () => HttpResponse.json(empty)))
    renderRoutes(routes, '/')
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'New request' }).every((a) => a.getAttribute('href') === '/requests/new')).toBe(true)
  })

  it('shows a skeleton while loading', async () => {
    server.use(
      http.get('/api/dashboard', async () => {
        await delay(50)
        return HttpResponse.json(empty)
      }),
    )
    renderRoutes(routes, '/')
    expect(await screen.findByRole('status', { name: 'Loading dashboard' })).toBeInTheDocument()
  })

  it('shows a mapped error with a working Retry', async () => {
    let calls = 0
    server.use(
      http.get('/api/dashboard', () => {
        calls += 1
        return calls <= 2
          ? HttpResponse.json({ status: 500, code: 'server.error', traceId: 't' }, { status: 500 })
          : HttpResponse.json(empty)
      }),
    )
    renderRoutes(routes, '/')
    expect(await screen.findByText(/Something went wrong on our side/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
  })
})
