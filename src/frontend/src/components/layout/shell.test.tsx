import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/routes'
import type { Capability } from '@/lib/api/types'
import { server } from '@/mocks/server'
import { setMockUser } from '@/mocks/state'
import type { MockUserKey } from '@/mocks/users'
import { renderRoutes } from '@/test/renderRoutes'
import { navigationFor } from './navigation'

const labels = (items: readonly { label: string }[]) => items.map((item) => item.label)

describe('navigationFor (capability → items)', () => {
  it('employee: requests.submit only', () => {
    const nav = navigationFor(['requests.submit'])
    expect(nav.canSubmitRequests).toBe(true)
    expect(labels(nav.primary)).toEqual(['Dashboard', 'My requests', 'Notifications'])
    expect(nav.admin).toEqual([])
  })

  it('manager / officer: tasks.view adds Team requests and Inbox, in spec order', () => {
    const nav = navigationFor(['requests.submit', 'tasks.view'])
    expect(labels(nav.primary)).toEqual(['Dashboard', 'My requests', 'Team requests', 'Inbox', 'Notifications'])
    expect(nav.admin).toEqual([])
  })

  it('administrator: admin.manage shows Administration but no request actions', () => {
    const nav = navigationFor(['admin.manage'])
    expect(nav.canSubmitRequests).toBe(false)
    expect(labels(nav.primary)).toEqual(['Dashboard', 'My requests', 'Notifications'])
    expect(labels(nav.admin)).toEqual(['Users', 'Departments', 'Stalled tasks'])
  })

  it('shows nothing extra for an empty capability list', () => {
    const nav = navigationFor([])
    expect(nav.canSubmitRequests).toBe(false)
    expect(labels(nav.primary)).toEqual(['Dashboard', 'My requests', 'Notifications'])
  })

  it('combines capabilities', () => {
    const all: Capability[] = ['requests.submit', 'tasks.view', 'admin.manage']
    const nav = navigationFor(all)
    expect(nav.primary).toHaveLength(5)
    expect(nav.admin).toHaveLength(3)
  })
})

const mainNav = () => screen.findByRole('navigation', { name: 'Main' })
/** The name of each link in order: its aria-label when it has one (the Inbox count), else its text. */
function linkNames(nav: HTMLElement): string[] {
  return within(nav)
    .getAllByRole('link')
    .map((link) => link.getAttribute('aria-label') ?? link.textContent?.trim() ?? '')
}

describe('sidebar per signed-in user', () => {
  const cases: Array<[MockUserKey, string[]]> = [
    ['employee', ['New request', 'Dashboard', 'My requests', 'Notifications']],
    ['manager', ['New request', 'Dashboard', 'My requests', 'Team requests', 'Inbox, 3 open', 'Notifications']],
    ['finance', ['New request', 'Dashboard', 'My requests', 'Team requests', 'Inbox, 3 open', 'Notifications']],
    ['admin', ['Dashboard', 'My requests', 'Notifications', 'Users', 'Departments', 'Stalled tasks']],
  ]

  it.each(cases)('%s sees exactly their items', async (user, expected) => {
    setMockUser(user)
    renderRoutes(routes, '/')
    const nav = await mainNav()
    // The Inbox count arrives asynchronously.
    if (expected.some((name) => name.startsWith('Inbox'))) {
      await within(nav).findByRole('link', { name: /, \d+ open$/ })
    }
    expect(linkNames(nav)).toEqual(expected)
  })

  it('announces the Inbox count as part of the link name', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/')
    const nav = await mainNav()
    await within(nav).findByRole('link', { name: /, \d+ open$/ })
    const inbox = within(nav).getAllByRole('link').find((link) => link.getAttribute('href') === '/inbox')
    expect(inbox).toHaveAccessibleName('Inbox, 3 open')
  })

  it('shows the Administration heading only to administrators', async () => {
    setMockUser('employee')
    renderRoutes(routes, '/')
    await mainNav()
    expect(screen.queryByRole('heading', { name: 'Administration' })).toBeNull()
  })

  it('marks the current page with aria-current', async () => {
    renderRoutes(routes, '/requests')
    const nav = await mainNav()
    expect(within(nav).getByRole('link', { name: 'My requests' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).not.toHaveAttribute('aria-current')
  })

  it('keeps "My requests" inactive on /requests/team', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/requests/team')
    const nav = await mainNav()
    expect(within(nav).getByRole('link', { name: 'Team requests' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'My requests' })).not.toHaveAttribute('aria-current')
  })

  it('still renders the navigation when the Inbox count cannot be loaded', async () => {
    setMockUser('manager')
    server.use(http.get('/api/tasks', () => new HttpResponse(null, { status: 500 })))
    renderRoutes(routes, '/')
    const nav = await mainNav()
    expect(within(nav).getByRole('link', { name: 'Inbox' })).toBeInTheDocument()
  })
})

describe('route guards follow the same capabilities', () => {
  const cases: Array<[MockUserKey, string, string]> = [
    ['employee', '/inbox', "You don't have access"],
    ['employee', '/requests/team', "You don't have access"],
    ['employee', '/admin/users', "You don't have access"],
    ['manager', '/inbox', 'Inbox'],
    ['manager', '/admin/departments', "You don't have access"],
    ['finance', '/requests/team', 'Team requests'],
    ['admin', '/admin/stalled-tasks', 'Stalled tasks'],
    ['admin', '/requests/new', "You don't have access"],
    ['employee', '/requests/new', 'New request'],
  ]

  it.each(cases)('%s opening %s sees "%s"', async (user, path, heading) => {
    setMockUser(user)
    renderRoutes(routes, path)
    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })
})

describe('frame', () => {
  it('has a skip link to the main content and the page landmarks', async () => {
    renderRoutes(routes, '/')
    await mainNav()
    expect(screen.getByRole('link', { name: 'Skip to content' })).toHaveAttribute('href', '#main')
    expect(screen.getAllByRole('main')).toHaveLength(1)
    // The top bar is a <header> outside <main>, so browsers expose it as the page banner.
    const topBar = screen.getByRole('button', { name: /Layla Haddad/ }).closest('header')
    expect(topBar).not.toBeNull()
    expect(screen.getByRole('main')).not.toContainElement(topBar)
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  })

  it('has the notifications bell in the top bar', async () => {
    renderRoutes(routes, '/')
    await mainNav()
    const topBar = screen.getByRole('button', { name: /Layla Haddad/ }).closest('header')
    expect(topBar).not.toBeNull()
    expect(within(topBar as HTMLElement).getByRole('button', { name: /^Notifications/ })).toHaveAttribute('aria-expanded', 'false')
  })
})

describe('user menu', () => {
  const button = () => screen.findByRole('button', { name: /Layla Haddad/ })

  it('opens with who you are, change password and sign out', async () => {
    renderRoutes(routes, '/')
    const trigger = await button()
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    await userEvent.click(trigger)
    expect(trigger).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('employee@flowops.test')).toBeInTheDocument()
    expect(screen.getByText('Engineering')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Change password' })).toHaveAttribute('href', '/change-password')
  })

  it('closes with Esc and returns focus to its button', async () => {
    renderRoutes(routes, '/')
    const trigger = await button()
    await userEvent.click(trigger)
    await userEvent.keyboard('{Tab}')
    await userEvent.keyboard('{Escape}')
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    expect(trigger).toHaveFocus()
  })

  it('closes on a click outside', async () => {
    renderRoutes(routes, '/')
    const trigger = await button()
    await userEvent.click(trigger)
    await userEvent.click(screen.getByRole('heading', { level: 1 }))
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
  })

  it('signs out and lands on the sign-in page', async () => {
    const { router } = renderRoutes(routes, '/')
    await userEvent.click(await button())
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('goes to change password from the menu', async () => {
    const { router } = renderRoutes(routes, '/')
    await userEvent.click(await button())
    await userEvent.click(screen.getByRole('link', { name: 'Change password' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Change password' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/change-password')
  })
})

describe('mobile drawer', () => {
  const openMenu = () => screen.findByRole('button', { name: 'Open menu' })

  it('is closed until the menu button is pressed, then shows the same navigation', async () => {
    setMockUser('manager')
    renderRoutes(routes, '/')
    expect(screen.queryByRole('dialog')).toBeNull()
    await userEvent.click(await openMenu())
    const drawer = screen.getByRole('dialog', { name: 'FlowOps' })
    const nav = within(drawer).getByRole('navigation', { name: 'Menu' })
    expect(within(nav).getByRole('link', { name: 'Team requests' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'New request' })).toBeInTheDocument()
  })

  it('opens with the keyboard and closes with Esc, returning focus to the menu button', async () => {
    renderRoutes(routes, '/')
    const button = await openMenu()
    button.focus()
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(button).toHaveFocus()
  })

  it('keeps Tab inside the drawer', async () => {
    renderRoutes(routes, '/')
    await userEvent.click(await openMenu())
    const drawer = screen.getByRole('dialog')
    for (let i = 0; i < 12; i++) {
      await userEvent.tab()
      expect(drawer).toContainElement(document.activeElement as HTMLElement)
    }
  })

  it('closes after following a link and shows that page', async () => {
    const { router } = renderRoutes(routes, '/')
    await userEvent.click(await openMenu())
    const drawer = screen.getByRole('dialog')
    await userEvent.click(within(drawer).getByRole('link', { name: 'My requests' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'My requests' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(router.state.location.pathname).toBe('/requests')
  })

  it('closes with its Close button', async () => {
    renderRoutes(routes, '/')
    await userEvent.click(await openMenu())
    await userEvent.click(screen.getByRole('button', { name: 'Close dialog' }))
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
