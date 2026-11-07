import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { server } from '@/mocks/server'
import { MOCK_REQUEST_ID } from '@/mocks/notificationStore'
import { setMockUser } from '@/mocks/state'
import { renderRoutes } from '@/test/renderRoutes'
import { notificationTarget } from './targets'

const page = () => screen.findByRole('heading', { level: 1, name: 'Notifications' })
const bell = () => screen.findByRole('button', { name: /^Notifications/ })

describe('notificationTarget', () => {
  const base = { requestId: null, taskId: null }
  it('sends role-change notifications to the role-changes screen, whatever the ids', () => {
    for (const type of ['RoleChangePending', 'RoleChangeApproved', 'RoleChangeRejected'] as const) {
      expect(notificationTarget({ ...base, type })).toBe('/admin/role-changes')
    }
  })
  it('follows the request id, falls back to the inbox for a bare task, and otherwise has no target', () => {
    expect(notificationTarget({ ...base, type: 'StepApproved', requestId: 'r1' })).toBe('/requests/r1')
    expect(notificationTarget({ ...base, type: 'TaskAssigned', taskId: 't1' })).toBe('/inbox')
    expect(notificationTarget({ ...base, type: 'CommentAdded' })).toBeNull()
  })
})

describe('Notifications page', () => {
  it('lists my notifications newest first with unread ones marked, and links to the request', async () => {
    setMockUser('employee')
    renderRoutes(routes, '/notifications')
    await page()
    const link = await screen.findByRole('link', { name: /Step approved/ })
    expect(link).toHaveAttribute('href', `/requests/${MOCK_REQUEST_ID}`)
    expect(link).toHaveTextContent('Unread:')
    expect(screen.getByRole('link', { name: /Request completed/ })).not.toHaveTextContent('Unread:')
  })

  it('filters to unread only, kept in the URL', async () => {
    setMockUser('employee')
    const { router } = renderRoutes(routes, '/notifications')
    await screen.findByRole('link', { name: /Request completed/ })
    await userEvent.click(screen.getByRole('checkbox', { name: 'Unread only' }))
    await waitFor(() => expect(screen.queryByRole('link', { name: /Request completed/ })).not.toBeInTheDocument())
    expect(router.state.location.search).toBe('?unread=1')
  })

  it('opening an unread notification marks it read at once, lowers the badge and navigates', async () => {
    setMockUser('employee')
    const { router } = renderRoutes(routes, '/notifications')
    expect(await screen.findByRole('button', { name: 'Notifications, 2 unread' })).toBeInTheDocument()
    await userEvent.click(await screen.findByRole('link', { name: /Step approved/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/requests/${MOCK_REQUEST_ID}`))
    expect(await screen.findByRole('button', { name: 'Notifications, 1 unread' })).toBeInTheDocument()
  })

  it('rolls the optimistic mark-read back and says so when the server refuses', async () => {
    setMockUser('employee')
    server.use(http.post('/api/notifications/:id/read', () => HttpResponse.json({ status: 500, code: 'unexpected' }, { status: 500 })))
    renderRoutes(routes, '/notifications')
    await screen.findByRole('button', { name: 'Notifications, 2 unread' })
    await userEvent.click(await screen.findByRole('link', { name: /Step approved/ }))
    expect(await screen.findByText('Could not mark the notification as read. Please try again.')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Notifications, 2 unread' })).toBeInTheDocument()
  })

  it('marks everything read', async () => {
    setMockUser('employee')
    renderRoutes(routes, '/notifications')
    await screen.findByRole('button', { name: 'Notifications, 2 unread' })
    await userEvent.click(screen.getByRole('button', { name: 'Mark all as read' }))
    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mark all as read' })).toBeDisabled()
  })

  it('shows an empty state for a user without notifications', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/notifications')
    expect(await screen.findByText('You have no notifications yet.')).toBeInTheDocument()
  })

  it('shows the error with a retry when loading fails', async () => {
    setMockUser('employee')
    server.use(http.get('/api/notifications', () => HttpResponse.json({ status: 500, code: 'unexpected' }, { status: 500 })))
    renderRoutes(routes, '/notifications')
    server.resetHandlers()
    server.use(http.get('/api/notifications', () => HttpResponse.json({ status: 500, code: 'unexpected' }, { status: 500 })))
    const retry = await screen.findByRole('button', { name: 'Retry' })
    server.resetHandlers()
    await userEvent.click(retry)
    expect(await screen.findByRole('link', { name: /Step approved/ })).toBeInTheDocument()
  })

  it('role-change notifications (no request or task) open the role-changes screen', async () => {
    setMockUser('admin')
    const { router } = renderRoutes(routes, '/notifications')
    await userEvent.click(await screen.findByRole('link', { name: /Role change awaiting approval/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/role-changes'))
  })
})

describe('Notification bell', () => {
  it('opens a popover with the latest notifications and a link to all of them', async () => {
    setMockUser('employee')
    renderRoutes(routes, '/requests')
    await userEvent.click(await screen.findByRole('button', { name: 'Notifications, 2 unread' }))
    const popover = await screen.findByRole('region', { name: 'Latest notifications' })
    expect(await within(popover).findByRole('link', { name: /Step approved/ })).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('region', { name: 'Latest notifications' })).not.toBeInTheDocument()
    await userEvent.click(await bell())
    await userEvent.click(await screen.findByRole('link', { name: 'View all' }))
    expect(await page()).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Latest notifications' })).not.toBeInTheDocument()
  })
})
