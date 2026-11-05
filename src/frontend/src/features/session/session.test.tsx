import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useQuery } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { RootLayout } from '@/app/RootLayout'
import { routes } from '@/app/routes'
import { ForbiddenPage } from '@/app/pages/ForbiddenPage'
import { api } from '@/lib/api/client'
import { queryKeys } from '@/lib/api/queryKeys'
import { MOCK_RATE_LIMITED_EMAIL, MOCK_PASSWORD } from '@/mocks/users'
import { setMockUser } from '@/mocks/state'
import { server } from '@/mocks/server'
import { renderRoutes } from '@/test/renderRoutes'
import { RequireAuth } from './components/RequireAuth'
import { RequireCapability } from './components/RequireCapability'
import { LoginPage } from './pages/LoginPage'

const heading = (name: string | RegExp) => screen.findByRole('heading', { level: 1, name })

async function signIn(email: string, password: string) {
  await userEvent.type(await screen.findByLabelText(/^Email/), email)
  await userEvent.type(screen.getByLabelText(/^Password/), password)
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('sign in', () => {
  it('sends an anonymous visitor to /login, keeping where they were going', async () => {
    setMockUser(null)
    const { router } = renderRoutes(routes, '/change-password')
    expect(await heading('Sign in')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.search).toBe('?returnTo=%2Fchange-password')
  })

  it('signs in and returns to the page they asked for', async () => {
    setMockUser(null)
    const { router } = renderRoutes(routes, '/change-password')
    await signIn('employee@flowops.test', MOCK_PASSWORD)
    expect(await heading('Change password')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/change-password')
  })

  it('goes to the dashboard when there is no returnTo', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    await signIn('employee@flowops.test', MOCK_PASSWORD)
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
  })

  it('ignores a returnTo that points off-site', async () => {
    setMockUser(null)
    const { router } = renderRoutes(routes, '/login?returnTo=%2F%2Fevil.example')
    await signIn('employee@flowops.test', MOCK_PASSWORD)
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/')
  })

  it('shows a generic message for wrong credentials and stays on the page', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    await signIn('employee@flowops.test', 'wrong-password')
    // (The toast region is also a role="alert", so find the banner by its text.)
    const banner = await screen.findByText(/^The email or password is incorrect\./)
    expect(banner).toHaveAttribute('role', 'alert')
    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })

  it('shows the rate-limit message (429)', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    await signIn(MOCK_RATE_LIMITED_EMAIL, MOCK_PASSWORD)
    const banner = await screen.findByText('Too many sign-in attempts. Please wait a few minutes and try again.')
    expect(banner).toHaveAttribute('role', 'alert')
  })

  it('validates empty fields, links the errors and focuses the first invalid field', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    await userEvent.click(await screen.findByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('Enter your email.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Email/)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText(/^Email/)).toHaveFocus()
    expect(screen.getByLabelText(/^Password/)).toHaveAccessibleDescription('Enter your password.')
  })

  it('submits with Enter', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    await userEvent.type(await screen.findByLabelText(/^Email/), 'manager@flowops.test')
    await userEvent.type(screen.getByLabelText(/^Password/), `${MOCK_PASSWORD}{Enter}`)
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
  })

  it('has autocomplete hints and no register link', async () => {
    setMockUser(null)
    renderRoutes(routes, '/login')
    expect(await screen.findByLabelText(/^Email/)).toHaveAttribute('autocomplete', 'username')
    expect(screen.getByLabelText(/^Password/)).toHaveAttribute('autocomplete', 'current-password')
    expect(screen.queryByRole('link', { name: /register|sign up|create account/i })).toBeNull()
  })

  it('sends a signed-in user away from /login', async () => {
    renderRoutes(routes, '/login')
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
  })

  it('shows a retryable error when the session cannot be read (server down)', async () => {
    server.use(http.get('/api/auth/me', () => new HttpResponse(null, { status: 503 })))
    renderRoutes(routes, '/')
    const message = await screen.findByText(/Something went wrong on our side/)
    expect(message.closest('[role="alert"]')).not.toBeNull()
    server.resetHandlers()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
  })
})

describe('forced password change', () => {
  const newPasswordInput = () => screen.getByLabelText(/^New password/)
  const confirmInput = () => screen.getByLabelText(/^Confirm new password/)
  const currentInput = () => screen.getByLabelText(/^Current password/)

  it('sends a must-change user to /change-password from any page', async () => {
    setMockUser('newHire')
    const { router } = renderRoutes(routes, '/')
    expect(await heading('Change password')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/change-password')
    expect(screen.getByText(/must choose a new password/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it('validates length and confirmation on the client', async () => {
    setMockUser('newHire')
    renderRoutes(routes, '/change-password')
    await userEvent.type(await screen.findByLabelText(/^Current password/), MOCK_PASSWORD)
    await userEvent.type(newPasswordInput(), 'short')
    await userEvent.type(confirmInput(), 'different')
    await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
    expect(await screen.findByText('Use at least 12 characters.')).toBeInTheDocument()
    expect(screen.getByText('The passwords do not match.')).toBeInTheDocument()
    expect(newPasswordInput()).toHaveAttribute('aria-invalid', 'true')
  })

  it('puts the server message on the current-password field', async () => {
    setMockUser('newHire')
    renderRoutes(routes, '/change-password')
    await userEvent.type(await screen.findByLabelText(/^Current password/), 'not-the-password')
    await userEvent.type(newPasswordInput(), 'a-brand-new-password')
    await userEvent.type(confirmInput(), 'a-brand-new-password')
    await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
    expect(await screen.findByText('The current password is incorrect.')).toBeInTheDocument()
    expect(currentInput()).toHaveAttribute('aria-invalid', 'true')
  })

  it('changes the password, confirms with a toast and releases the user to the app', async () => {
    setMockUser('newHire')
    const { router } = renderRoutes(routes, '/change-password')
    await userEvent.type(await screen.findByLabelText(/^Current password/), MOCK_PASSWORD)
    await userEvent.type(newPasswordInput(), 'a-brand-new-password')
    await userEvent.type(confirmInput(), 'a-brand-new-password')
    await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
    expect(await heading('FlowOps foundation')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByRole('status')).toHaveTextContent('Password changed.')
  })

  it('lets a must-change user sign out', async () => {
    setMockUser('newHire')
    const { router } = renderRoutes(routes, '/change-password')
    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }))
    expect(await heading('Sign in')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('shows a normal user a Cancel button instead of Sign out', async () => {
    renderRoutes(routes, '/change-password')
    expect(await heading('Change password')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
    expect(screen.queryByText(/must choose a new password/i)).toBeNull()
  })
})

describe('error pages', () => {
  it('shows 404 for an unknown route with a way home', async () => {
    renderRoutes(routes, '/no/such/page')
    expect(await heading('Page not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to dashboard' })).toHaveAttribute('href', '/')
  })

  it('shows 403 with a way home', async () => {
    renderRoutes(routes, '/forbidden')
    expect(await heading("You don't have access")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to dashboard' })).toHaveAttribute('href', '/')
  })
})

describe('capability guard', () => {
  const guarded = [
    {
      element: <RootLayout />,
      children: [
        { path: '/login', element: <LoginPage /> },
        { path: '/forbidden', element: <ForbiddenPage /> },
        {
          element: <RequireAuth />,
          children: [
            {
              element: <RequireCapability capability="admin.manage" />,
              children: [{ path: '/admin', element: <h1>Administration</h1> }],
            },
          ],
        },
      ],
    },
  ]

  it('sends a user without the capability to /forbidden', async () => {
    const { router } = renderRoutes(guarded, '/admin')
    expect(await heading("You don't have access")).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/forbidden')
  })

  it('lets a user with the capability in (the server grants it, not the role name)', async () => {
    setMockUser('admin')
    renderRoutes(guarded, '/admin')
    expect(await heading('Administration')).toBeInTheDocument()
  })

  it('sends an anonymous visitor to sign in, not to /forbidden', async () => {
    setMockUser(null)
    const { router } = renderRoutes(guarded, '/admin')
    expect(await heading('Sign in')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?returnTo=%2Fadmin')
  })
})

describe('session expiry', () => {
  function Probe() {
    const { data } = useQuery({
      queryKey: queryKeys.notifications.unread,
      queryFn: async () => (await api.GET('/api/notifications/unread-count')).data,
    })
    return <h1>Unread {data?.count ?? '…'}</h1>
  }
  const probeRoutes = [
    {
      element: <RootLayout />,
      children: [
        { path: '/login', element: <LoginPage /> },
        { element: <RequireAuth />, children: [{ path: '/probe', element: <Probe /> }] },
      ],
    },
  ]

  it('on a 401 mid-session: clears the cache, explains, and returns to the same page after sign in', async () => {
    const { router, queryClient } = renderRoutes(probeRoutes, '/probe?tab=1')
    expect(await heading('Unread 2')).toBeInTheDocument()

    setMockUser(null) // the server session ends
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all })
    })

    expect(await heading('Sign in')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.search).toBe('?returnTo=%2Fprobe%3Ftab%3D1')
    expect(screen.getByRole('status')).toHaveTextContent('Your session has ended. Please sign in again.')
    expect(queryClient.getQueryData(queryKeys.notifications.unread)).toBeUndefined()

    await signIn('employee@flowops.test', MOCK_PASSWORD)
    expect(await heading('Unread 2')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/probe')
    expect(router.state.location.search).toBe('?tab=1')
  })

  it('does not treat a wrong password at sign-in as an expired session', async () => {
    setMockUser(null)
    renderRoutes(probeRoutes, '/login')
    await signIn('employee@flowops.test', 'bad-password')
    await screen.findByText(/^The email or password is incorrect\./)
    // No "session ended" toast, and still on the sign-in page.
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })
})
