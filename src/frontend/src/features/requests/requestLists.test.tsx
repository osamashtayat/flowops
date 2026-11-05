import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'

const heading = (name: string) => screen.findByRole('heading', { level: 1, name })
const table = (name: string) => screen.findByRole('table', { name })

/** Records the query string of every list request. */
function recordRequests() {
  const urls: URL[] = []
  server.use(
    http.get('/api/requests', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    }),
  )
  return urls
}

describe('My requests', () => {
  it('lists my requests with number, title, type, status, stage and date, linking to the details', async () => {
    renderRoutes(routes, '/requests')
    await heading('My requests')
    const grid = await table('My requests')
    const rows = await within(grid).findAllByRole('row')
    expect(rows.length).toBe(21) // header + 20 (default page size)
    expect(within(grid).getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
      'Number',
      'Title',
      'Type',
      'Status',
      'Stage',
      'Submitted',
    ])
    const link = within(grid).getAllByRole('link')[0]
    expect(link).toHaveAttribute('href', expect.stringMatching(/^\/requests\/[\w-]+$/))
    expect(within(grid).getAllByText('Pending review').length).toBeGreaterThan(0)
    expect(within(grid).getAllByText('Finance approval').length).toBeGreaterThan(0)
  })

  it('shows a skeleton while loading', async () => {
    server.use(
      http.get('/api/requests', async () => {
        await delay(50)
        return HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })
      }),
    )
    renderRoutes(routes, '/requests')
    expect(await screen.findByText('Loading…')).toBeInTheDocument()
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
  })

  it('welcomes a first-time user with a way to start', async () => {
    recordRequests()
    renderRoutes(routes, '/requests')
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'New request' }).every((a) => a.getAttribute('href') === '/requests/new')).toBe(true)
  })

  it('shows a mapped error with a working Retry', async () => {
    let calls = 0
    server.use(
      http.get('/api/requests', () => {
        calls += 1
        return calls <= 2
          ? HttpResponse.json({ status: 500, code: 'server.error', traceId: 't' }, { status: 500 })
          : HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })
      }),
    )
    renderRoutes(routes, '/requests')
    expect(await screen.findByText(/Something went wrong on our side|server/i)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
  })

  it('keeps filters in the URL and sends them to the API', async () => {
    const urls = recordRequests()
    const { router } = renderRoutes(routes, '/requests')
    await userEvent.selectOptions(await screen.findByLabelText('Status'), 'Approved')
    await userEvent.selectOptions(screen.getByLabelText('Type'), 'Leave')
    await waitFor(() => expect(router.state.location.search).toBe('?status=Approved&type=Leave'))
    const last = urls.at(-1)
    expect(last?.searchParams.get('Scope')).toBe('mine')
    expect(last?.searchParams.get('Status')).toBe('Approved')
    expect(last?.searchParams.get('Type')).toBe('Leave')
  })

  it('restores filters and page from a shared link', async () => {
    const urls = recordRequests()
    renderRoutes(routes, '/requests?status=Rejected&page=2&pageSize=10&search=lap')
    expect(await screen.findByLabelText('Status')).toHaveValue('Rejected')
    expect(screen.getByLabelText('Search')).toHaveValue('lap')
    await waitFor(() => expect(urls.length).toBeGreaterThan(0))
    expect(urls[0]?.searchParams.get('Page')).toBe('2')
    expect(urls[0]?.searchParams.get('PageSize')).toBe('10')
    expect(urls[0]?.searchParams.get('Search')).toBe('lap')
  })

  it('ignores values in the URL the contract does not know', async () => {
    const urls = recordRequests()
    renderRoutes(routes, '/requests?status=Banana&type=Spaceship')
    await waitFor(() => expect(urls.length).toBeGreaterThan(0))
    expect(urls[0]?.searchParams.has('Status')).toBe(false)
    expect(urls[0]?.searchParams.has('Type')).toBe(false)
  })

  it('waits for a pause in typing before searching', async () => {
    const urls = recordRequests()
    const { router } = renderRoutes(routes, '/requests')
    await userEvent.type(await screen.findByLabelText('Search'), 'laptop')
    await waitFor(() => expect(router.state.location.search).toBe('?search=laptop'))
    await waitFor(() => expect(urls.some((u) => u.searchParams.has('Search'))).toBe(true))
    const searches = urls.map((u) => u.searchParams.get('Search')).filter(Boolean)
    expect(searches).toEqual(['laptop']) // no request for "l", "la", "lap"…
  })

  it('explains an empty result under filters and clears them', async () => {
    recordRequests()
    const { router } = renderRoutes(routes, '/requests?status=Approved')
    expect(await screen.findByText('No requests match these filters.')).toBeInTheDocument()
    const empty = screen.getByText('No requests match these filters.').parentElement as HTMLElement
    await userEvent.click(within(empty).getByRole('button', { name: 'Clear filters' }))
    await waitFor(() => expect(router.state.location.search).toBe(''))
    expect(await screen.findByText('You have no requests yet.')).toBeInTheDocument()
  })

  it('pages through the list and goes back to page 1 when a filter changes', async () => {
    const { router } = renderRoutes(routes, '/requests')
    await table('My requests')
    await userEvent.click(await screen.findByRole('button', { name: 'Next' }))
    await waitFor(() => expect(router.state.location.search).toBe('?page=2'))
    await userEvent.selectOptions(screen.getByLabelText('Status'), 'InReview')
    await waitFor(() => expect(router.state.location.search).toBe('?status=InReview'))
  })

  it('says so when the page is past the end, instead of "no requests"', async () => {
    const { router } = renderRoutes(routes, '/requests?page=99')
    expect(await screen.findByText('This page is past the end of the list.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Go to the first page' }))
    await waitFor(() => expect(router.state.location.search).toBe(''))
    expect(await table('My requests')).toBeInTheDocument()
  })

  it('finds a request by a number prefix', async () => {
    renderRoutes(routes, '/requests?search=FO-2026-000800')
    const grid = await table('My requests')
    await waitFor(() => expect(within(grid).getAllByRole('row')).toHaveLength(2))
  })

  it('hides New request from someone who cannot submit', async () => {
    setMockUser('admin')
    recordRequests()
    renderRoutes(routes, '/requests')
    await screen.findByText('You have no requests yet.')
    expect(screen.queryByRole('link', { name: 'New request' })).not.toBeInTheDocument()
  })
})

describe('Team requests', () => {
  it('sends someone without tasks.view to /forbidden', async () => {
    const { router } = renderRoutes(routes, '/requests/team')
    await waitFor(() => expect(router.state.location.pathname).toBe('/forbidden'))
  })

  it('asks for scope=visible and adds the Requester column', async () => {
    setMockUser('manager')
    const urls: URL[] = []
    server.events.on('request:start', ({ request }) => {
      if (request.url.includes('/api/requests')) urls.push(new URL(request.url))
    })
    renderRoutes(routes, '/requests/team')
    await heading('Team requests')
    const grid = await table('Team requests')
    expect(within(grid).getAllByRole('columnheader').map((h) => h.textContent)).toContain('Requester')
    expect(within(grid).getAllByText('Layla Haddad').length).toBeGreaterThan(0)
    expect(urls[0]?.searchParams.get('Scope')).toBe('visible')
    server.events.removeAllListeners()
  })
})
