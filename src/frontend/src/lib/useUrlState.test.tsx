import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import { describe, expect, it } from 'vitest'
import { useUrlState } from './useUrlState'

const defaults = { page: 1, pageSize: 20, status: '' }

function Probe() {
  const [state, setState] = useUrlState(defaults)
  const { search } = useLocation()
  return (
    <>
      <output data-testid="state">{JSON.stringify(state)}</output>
      <output data-testid="search">{search}</output>
      <button onClick={() => setState({ page: 3 })}>page 3</button>
      <button onClick={() => setState({ status: 'Approved' })}>filter</button>
      <button onClick={() => setState({ pageSize: 50 })}>size</button>
      <button onClick={() => setState({ status: '', page: 1 })}>reset</button>
    </>
  )
}

const renderAt = (url: string) =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Probe />
    </MemoryRouter>,
  )
const state = () => JSON.parse(screen.getByTestId('state').textContent ?? '{}') as typeof defaults
const search = () => screen.getByTestId('search').textContent

describe('useUrlState', () => {
  it('returns defaults for an empty URL', () => {
    renderAt('/requests')
    expect(state()).toEqual(defaults)
  })

  it('reads typed values from the URL', () => {
    renderAt('/requests?page=4&pageSize=50&status=Approved')
    expect(state()).toEqual({ page: 4, pageSize: 50, status: 'Approved' })
  })

  it.each(['page=abc', 'page=0', 'page=-2', 'page=1.5', 'page=99999999999999999999', 'page='])(
    'falls back to the default for a bad number (%s)',
    (query) => {
      renderAt(`/requests?${query}`)
      expect(state().page).toBe(1)
    },
  )

  it('writes changes to the URL and reads them back', async () => {
    renderAt('/requests')
    await userEvent.click(screen.getByRole('button', { name: 'page 3' }))
    expect(search()).toBe('?page=3')
    expect(state().page).toBe(3)
  })

  it('sends page back to 1 when a filter or the page size changes', async () => {
    renderAt('/requests?page=4')
    await userEvent.click(screen.getByRole('button', { name: 'filter' }))
    expect(search()).toBe('?status=Approved')
    expect(state()).toEqual({ page: 1, pageSize: 20, status: 'Approved' })

    await userEvent.click(screen.getByRole('button', { name: 'page 3' }))
    await userEvent.click(screen.getByRole('button', { name: 'size' }))
    expect(state().page).toBe(1)
    expect(search()).toBe('?status=Approved&pageSize=50')
  })

  it('keeps default values out of the URL', async () => {
    renderAt('/requests?status=Approved&page=2')
    await userEvent.click(screen.getByRole('button', { name: 'reset' }))
    expect(search()).toBe('')
  })
})
