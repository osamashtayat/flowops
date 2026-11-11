import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { getMockRequestFor } from '@/mocks/requestStore'
import { MOCK_USERS } from '@/mocks/users'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'
import { queryKeys } from '@/lib/api/queryKeys'

/** Seed 1: a purchase in Finance approval from the mock employee. Seed 3 is a leave. */
const PURCHASE = '5eed0000-0000-4000-8000-000000000001'
const LEAVE = '5eed0000-0000-4000-8000-000000000003'
const PURCHASE_PATH = `/requests/${PURCHASE}`

const problem = (status: number, code: string) => HttpResponse.json({ status, code, traceId: 't' }, { status })
const heading = (name: string | RegExp) => screen.findByRole('heading', { level: 1, name })

/** The mock request exactly as the server would send it to this user, with changes applied. */
function detailsFor(user: keyof typeof MOCK_USERS, id: string, change: (r: NonNullable<ReturnType<typeof getMockRequestFor>>) => object) {
  const request = getMockRequestFor(MOCK_USERS[user], id)
  if (!request) throw new Error('seed missing')
  return { ...request, ...change(request) }
}

describe('request details', () => {
  it('shows the header, details, steps, summary and activity of a purchase', async () => {
    renderRoutes(routes, PURCHASE_PATH)
    expect(await heading('FO-2026-000800 · Dell XPS 15 laptop')).toBeInTheDocument()
    expect(screen.getByText('Waiting for: Finance approval')).toBeInTheDocument()
    expect(screen.getByText('Estimated cost')).toBeInTheDocument()
    expect(screen.getByText('100.000 JOD')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Route' })).toBeInTheDocument()
    expect(screen.getByText('Looks reasonable.')).toBeInTheDocument()
    expect(screen.getByText('Waiting for Any of Finance officers')).toBeInTheDocument()
    expect(await screen.findByText(/submitted the request/)).toBeInTheDocument()
    expect(screen.getByText('Layla Haddad', { selector: 'dd' })).toBeInTheDocument()
  })

  it('shows a skeleton while loading', async () => {
    server.use(
      http.get('/api/requests/:id', async () => {
        await delay(50)
        return problem(500, 'server.error')
      }),
    )
    renderRoutes(routes, PURCHASE_PATH)
    expect(await screen.findByRole('status', { name: 'Loading request' })).toBeInTheDocument()
  })

  it('says "not found" for a 404, without revealing which kind', async () => {
    renderRoutes(routes, '/requests/does-not-exist')
    expect(await screen.findByText("This request doesn't exist or you don't have access.")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to my requests' })).toBeInTheDocument()
  })

  it('shows a mapped error with a working Retry', async () => {
    let calls = 0
    server.use(
      http.get('/api/requests/:id', ({ params }) => {
        calls += 1
        return calls <= 2 ? problem(500, 'server.error') : HttpResponse.json(getMockRequestFor(MOCK_USERS.employee, String(params.id)))
      }),
    )
    renderRoutes(routes, PURCHASE_PATH)
    expect(await screen.findByText(/Something went wrong on our side/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await heading(/FO-2026-000800/)).toBeInTheDocument()
  })

  it('shows a one-day leave as a single date, not "3 May – 3 May"', async () => {
    server.use(
      http.get('/api/requests/:id', ({ params }) => {
        const request = getMockRequestFor(MOCK_USERS.employee, String(params.id))
        return HttpResponse.json(request?.leave ? { ...request, leave: { ...request.leave, startDate: '2027-05-03', endDate: '2027-05-03', days: 1 } } : request)
      }),
    )
    renderRoutes(routes, `/requests/${LEAVE}`)
    const dates = await screen.findByText(/^3 May 2027$/)
    expect(dates.parentElement).toHaveTextContent('3 May 2027 (1 calendar day)')
  })

  describe('leave reason', () => {
    it('shows the reason to the requester', async () => {
      renderRoutes(routes, `/requests/${LEAVE}`)
      expect(await screen.findByText('Family trip')).toBeInTheDocument()
    })

    it('shows "Restricted" when the server hides it', async () => {
      setMockUser('finance')
      renderRoutes(routes, `/requests/${LEAVE}`)
      expect(await screen.findByText(/^Restricted\./)).toBeInTheDocument()
      expect(screen.queryByText('Family trip')).not.toBeInTheDocument()
    })
  })

  describe('actions come only from availableActions', () => {
    it('gives the requester no decision buttons', async () => {
      renderRoutes(routes, PURCHASE_PATH)
      await heading(/FO-2026-000800/)
      expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument()
    })

    it('offers Approve and Reject to the eligible finance officer, and nothing else', async () => {
      setMockUser('finance')
      renderRoutes(routes, PURCHASE_PATH)
      expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Fulfill' })).not.toBeInTheDocument()
    })

    it('renders exactly the actions the server lists', async () => {
      setMockUser('finance')
      server.use(
        http.get('/api/requests/:id', () =>
          HttpResponse.json(detailsFor('finance', PURCHASE, (r) => ({ availableActions: { taskId: r.tasks[1]?.id, actions: ['fulfill'], version: 'v1' } }))),
        ),
      )
      renderRoutes(routes, PURCHASE_PATH)
      expect(await screen.findByRole('button', { name: 'Fulfill' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    })

    it('shows the server\'s step name and uses one approve verb whatever the step is called', async () => {
      setMockUser('finance')
      const legal = { key: 'legal-review', name: 'Legal review' }
      server.use(
        http.get('/api/requests/:id', () =>
          HttpResponse.json(
            detailsFor('finance', PURCHASE, (r) => ({
              step: legal,
              tasks: [{ ...r.tasks[1], step: legal }],
              availableActions: { taskId: r.tasks[1]?.id, actions: ['approve'], version: 'v1' },
            })),
          ),
        ),
      )
      renderRoutes(routes, PURCHASE_PATH)
      expect(await screen.findByText('Waiting for: Legal review')).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument()
      // The dialog doesn't claim what comes next; the step order is the server's.
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      expect(await within(await screen.findByRole('dialog')).findByText(/Your decision is recorded in the request history/)).toBeInTheDocument()
    })
  })

  describe('decisions', () => {
    it('approves after confirming, then shows the next step without the buttons', async () => {
      setMockUser('finance')
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      const dialog = await screen.findByRole('dialog', { name: 'Approve FO-2026-000800?' })
      await userEvent.type(within(dialog).getByLabelText(/Comment/), 'Budget confirmed')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Approve request' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect((await screen.findAllByText('Budget confirmed')).length).toBeGreaterThan(0) // in the step and in the history
      expect(screen.queryByText(/^Waiting for:/)).not.toBeInTheDocument() // now Approved, awaiting fulfilment
      expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    })

    it('needs a reason to reject, and sends it', async () => {
      let rejectBody: unknown
      setMockUser('finance')
      server.use(
        http.post('/api/tasks/:taskId/reject', async ({ request }) => {
          rejectBody = await request.json()
          return new HttpResponse(null, { status: 204 })
        }),
      )
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
      const dialog = await screen.findByRole('dialog', { name: 'Reject FO-2026-000800?' })
      await userEvent.click(within(dialog).getByRole('button', { name: 'Reject request' }))
      expect(await within(dialog).findByText('Enter a reason.')).toBeInTheDocument()
      expect(rejectBody).toBeUndefined()

      await userEvent.type(within(dialog).getByLabelText(/^Reason/), 'Over budget')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Reject request' }))
      await waitFor(() => expect(rejectBody).toEqual({ version: 'v1', reason: 'Over budget' }))
    })

    it('sends the version the server gave it', async () => {
      let body: unknown
      setMockUser('finance')
      server.use(
        http.post('/api/tasks/:taskId/approve', async ({ request }) => {
          body = await request.json()
          return new HttpResponse(null, { status: 204 })
        }),
      )
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      await userEvent.click(await screen.findByRole('button', { name: 'Approve request' }))
      await waitFor(() => expect(body).toEqual({ version: 'v1', comment: null }))
    })

    it('says so when the decision is no longer this user\'s while its dialog is open (#67)', async () => {
      setMockUser('finance')
      const { queryClient } = renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
      await userEvent.type(within(await screen.findByRole('dialog')).getByLabelText(/^Reason/), 'Over budget')
      server.use(http.get('/api/requests/:id', () => HttpResponse.json(detailsFor('finance', PURCHASE, () => ({ availableActions: null })))))
      await queryClient.invalidateQueries({ queryKey: queryKeys.requests.detail(PURCHASE) })
      expect(await screen.findByText('FO-2026-000800 is no longer waiting for your decision, so the dialog was closed.')).toBeInTheDocument()
      expect(screen.queryByRole('dialog')).toBeNull()
    })

    it('closes an open dialog when a refresh hands the page a different task', async () => {
      setMockUser('finance')
      const { queryClient } = renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      await screen.findByRole('dialog')
      server.use(
        http.get('/api/requests/:id', () =>
          HttpResponse.json(detailsFor('finance', PURCHASE, (r) => ({ availableActions: { ...r.availableActions, taskId: 'another-task', version: 'v9' }, tasks: [...r.tasks, { ...r.tasks[1], id: 'another-task' }] }))),
        ),
      )
      await queryClient.invalidateQueries({ queryKey: queryKeys.requests.detail(PURCHASE) })
      expect(await screen.findByText(/no longer waiting for your decision/)).toBeInTheDocument()
      expect(screen.queryByRole('dialog')).toBeNull()
      expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument()
    })

    it('on a 409 shows the mapped message, closes the dialog and refetches', async () => {
      setMockUser('finance')
      let detailCalls = 0
      server.use(
        http.get('/api/requests/:id', ({ params }) => {
          detailCalls += 1
          return HttpResponse.json(getMockRequestFor(MOCK_USERS.finance, String(params.id)))
        }),
        http.post('/api/tasks/:taskId/approve', () => problem(409, 'task.already_decided')),
      )
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      await userEvent.click(await screen.findByRole('button', { name: 'Approve request' }))

      expect(await screen.findByText('Someone else already handled this task. The inbox has been refreshed.')).toBeInTheDocument()
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      await waitFor(() => expect(detailCalls).toBeGreaterThanOrEqual(2))
    })

    it('keeps the dialog and the typed text after a server error', async () => {
      setMockUser('finance')
      server.use(http.post('/api/tasks/:taskId/reject', () => problem(500, 'server.error')))
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
      const dialog = await screen.findByRole('dialog')
      await userEvent.type(within(dialog).getByLabelText(/^Reason/), 'Over budget')
      await userEvent.click(within(dialog).getByRole('button', { name: 'Reject request' }))
      expect(await within(dialog).findByText(/Something went wrong on our side/)).toBeInTheDocument()
      expect(within(dialog).getByLabelText(/^Reason/)).toHaveValue('Over budget')
    })

    it('can be cancelled without calling the API', async () => {
      let called = false
      setMockUser('finance')
      server.use(http.post('/api/tasks/:taskId/approve', () => ((called = true), new HttpResponse(null, { status: 204 }))))
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
      await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(called).toBe(false)
    })
  })

  describe('comments', () => {
    it('posts a comment and shows it as plain text', async () => {
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.type(await screen.findByLabelText('Add a comment'), '<b>Thanks</b> all')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))
      expect(await screen.findByText('<b>Thanks</b> all')).toBeInTheDocument()
      expect(screen.getByLabelText('Add a comment')).toHaveValue('')
    })

    it('does not send an empty comment', async () => {
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.click(await screen.findByRole('button', { name: 'Post comment' }))
      expect(await screen.findByText('Enter a comment.')).toBeInTheDocument()
    })

    it('shows a server 400 on the comment field', async () => {
      server.use(
        http.post('/api/requests/:id/comments', () =>
          HttpResponse.json({ status: 400, code: 'validation', traceId: 't', errors: { body: ['That comment is not allowed.'] } }, { status: 400 }),
        ),
      )
      renderRoutes(routes, PURCHASE_PATH)
      await userEvent.type(await screen.findByLabelText('Add a comment'), 'hello')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))
      expect(await screen.findByText('That comment is not allowed.')).toBeInTheDocument()
    })

    it('hides the box when the server says canComment is false', async () => {
      server.use(http.get('/api/requests/:id', () => HttpResponse.json(detailsFor('employee', PURCHASE, () => ({ canComment: false })))))
      renderRoutes(routes, PURCHASE_PATH)
      await heading(/FO-2026-000800/)
      expect(screen.queryByLabelText('Add a comment')).not.toBeInTheDocument()
    })
  })
})
