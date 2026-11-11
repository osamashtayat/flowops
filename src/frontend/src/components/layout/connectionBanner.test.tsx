import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'

const UNREACHABLE = /Can't reach FlowOps right now/
const OFFLINE = /You're offline/
const dashboard = () => screen.findByRole('heading', { level: 1, name: 'Dashboard' })
// Long automatic delays: these tests drive the checks themselves.
const manualChecks = { retryDelaysMs: [60_000] }

describe('connection banner', () => {
  it('server unreachable at start: explains, and loads the page after Try again', async () => {
    server.use(
      http.get('/api/auth/me', () => HttpResponse.error()),
      http.get('/api/health', () => HttpResponse.error()),
    )
    renderRoutes(routes, '/', manualChecks)
    expect(await screen.findByText(UNREACHABLE)).toBeInTheDocument()
    // Paused, not failed: no error screen while the banner explains.
    expect(screen.queryByRole('button', { name: 'Retry' })).toBeNull()

    server.resetHandlers()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await dashboard()).toBeInTheDocument()
    expect(screen.queryByText(UNREACHABLE)).toBeNull()
    expect(await screen.findByText("You're back online.")).toBeInTheDocument()
  })

  it('treats a gateway failure (API behind the proxy is down) as unreachable', async () => {
    server.use(http.get('/api/auth/me', () => new HttpResponse(null, { status: 502 })))
    renderRoutes(routes, '/', manualChecks)
    expect(await screen.findByText(UNREACHABLE)).toBeInTheDocument()
  })

  it('browser goes offline mid-session: shows it, and clears it once the server answers again', async () => {
    renderRoutes(routes, '/', manualChecks)
    expect(await dashboard()).toBeInTheDocument()
    expect(screen.queryByText(OFFLINE)).toBeNull()

    act(() => void window.dispatchEvent(new Event('offline')))
    expect(await screen.findByText(OFFLINE)).toBeInTheDocument()

    act(() => void window.dispatchEvent(new Event('online')))
    expect(await screen.findByText("You're back online.")).toBeInTheDocument()
    expect(screen.queryByText(OFFLINE)).toBeNull()
  })

  it('stays up while the health check still fails', async () => {
    server.use(http.get('/api/auth/me', () => HttpResponse.error()), http.get('/api/health', () => HttpResponse.error()))
    renderRoutes(routes, '/', manualChecks)
    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('button', { name: 'Try again' })).toBeEnabled()
    expect(screen.getByText(UNREACHABLE)).toBeInTheDocument()
  })
})
