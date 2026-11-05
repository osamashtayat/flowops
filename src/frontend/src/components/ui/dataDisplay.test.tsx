import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DataTable, type DataTableColumn } from './DataTable'
import { EmptyState } from './EmptyState'
import { ErrorState } from './ErrorState'
import { FilterBar } from './FilterBar'
import { Pagination } from './Pagination'

interface Row {
  id: string
  number: string
  cost: string
}
const rows: Row[] = [
  { id: '1', number: 'FO-2026-000001', cost: '1,250.500 JOD' },
  { id: '2', number: 'FO-2026-000002', cost: '30.000 JOD' },
]
const columns: DataTableColumn<Row>[] = [
  { key: 'number', header: 'Request', isRowHeader: true, cell: (r) => <a href={`/requests/${r.id}`}>{r.number}</a> },
  { key: 'cost', header: 'Estimated cost', align: 'right', cell: (r) => r.cost },
]
const baseProps = { caption: 'My requests', columns, getRowKey: (r: Row) => r.id }

describe('DataTable', () => {
  it('renders rows with a named table, column headers and a row header link', () => {
    render(<DataTable {...baseProps} rows={rows} />)
    const table = screen.getByRole('table', { name: 'My requests' })
    expect(within(table).getByRole('columnheader', { name: 'Estimated cost' })).toBeInTheDocument()
    expect(within(table).getByRole('rowheader', { name: 'FO-2026-000001' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'FO-2026-000002' })).toHaveAttribute('href', '/requests/2')
  })

  it('right-aligns numeric columns with tabular figures and labels cells for the stacked layout', () => {
    render(<DataTable {...baseProps} rows={rows} />)
    const cost = screen.getByRole('cell', { name: '1,250.500 JOD' })
    expect(cost).toHaveClass('text-right', 'tabular-nums')
    expect(cost).toHaveAttribute('data-label', 'Estimated cost')
  })

  it('shows skeleton rows and a loading status while loading, without exposing fake data', () => {
    render(<DataTable {...baseProps} rows={[]} isLoading skeletonRows={3} />)
    expect(screen.getByRole('status')).toHaveTextContent('Loading…')
    expect(screen.getByRole('table', { name: 'My requests' })).toHaveAttribute('aria-busy', 'true')
    expect(screen.queryByText('Nothing to show.')).toBeNull()
    // header row + 3 skeleton rows (hidden from AT)
    expect(document.querySelectorAll('tbody tr')).toHaveLength(3)
  })

  it('shows the empty slot when there are no rows', () => {
    render(<DataTable {...baseProps} rows={[]} empty={<EmptyState message="You have no requests yet." />} />)
    expect(screen.getByText('You have no requests yet.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('has a sensible default empty message', () => {
    render(<DataTable {...baseProps} rows={[]} />)
    expect(screen.getByText('Nothing to show.')).toBeInTheDocument()
  })

  it('shows the error with a working Retry instead of the table', async () => {
    const onRetry = vi.fn<() => void>()
    render(<DataTable {...baseProps} rows={rows} error={{ message: 'Could not load requests.', onRetry }} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Could not load requests.')
    expect(screen.queryByRole('table')).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(onRetry).toHaveBeenCalledOnce()
  })
})

describe('EmptyState / ErrorState', () => {
  it('EmptyState shows its message and optional action', () => {
    render(<EmptyState message="No requests." action={<button type="button">New request</button>} />)
    expect(screen.getByText('No requests.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New request' })).toBeInTheDocument()
  })

  it('ErrorState has no Retry button without a handler', () => {
    render(<ErrorState message="Something went wrong." />)
    expect(screen.queryByRole('button')).toBeNull()
  })
})

describe('Pagination', () => {
  const setup = (props: Partial<Parameters<typeof Pagination>[0]> = {}) => {
    const onPageChange = vi.fn<(page: number) => void>()
    const onPageSizeChange = vi.fn<(size: number) => void>()
    render(
      <Pagination
        page={2}
        pageSize={20}
        totalCount={132}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
        {...props}
      />,
    )
    return { onPageChange, onPageSizeChange }
  }

  it('shows the visible range', () => {
    setup()
    expect(screen.getByText('Showing 21–40 of 132')).toBeInTheDocument()
  })

  it('clamps the last range to the total', () => {
    setup({ page: 7 })
    expect(screen.getByText('Showing 121–132 of 132')).toBeInTheDocument()
  })

  it('reports no results and disables both buttons when empty', () => {
    setup({ page: 1, totalCount: 0 })
    expect(screen.getByText('No results')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('disables Previous on the first page and Next on the last', () => {
    setup({ page: 1 })
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
  })

  it('disables Next on the last page', () => {
    setup({ page: 7 })
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('moves between pages with the keyboard', async () => {
    const { onPageChange } = setup()
    await userEvent.tab() // the page-size select comes first
    await userEvent.tab()
    expect(screen.getByRole('button', { name: 'Previous' })).toHaveFocus()
    await userEvent.keyboard('{Enter}')
    expect(onPageChange).toHaveBeenLastCalledWith(1)
    await userEvent.tab()
    await userEvent.keyboard(' ')
    expect(onPageChange).toHaveBeenLastCalledWith(3)
  })

  it('reports a new page size as a number', async () => {
    const { onPageSizeChange } = setup()
    await userEvent.selectOptions(screen.getByLabelText('Rows per page'), '50')
    expect(onPageSizeChange).toHaveBeenCalledWith(50)
  })
})

describe('FilterBar', () => {
  it('is a named search region and offers Clear only when filters are active', async () => {
    const onClear = vi.fn<() => void>()
    const { rerender } = render(
      <FilterBar onClear={onClear}>
        <input aria-label="Status" />
      </FilterBar>,
    )
    // jsdom does not map <search> to the "search" landmark role (browsers do), so check the element.
    const region = screen.getByLabelText('Filters', { selector: 'search' })
    expect(region.tagName).toBe('SEARCH')
    expect(screen.queryByRole('button', { name: 'Clear filters' })).toBeNull()

    rerender(
      <FilterBar hasActiveFilters onClear={onClear}>
        <input aria-label="Status" />
      </FilterBar>,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(onClear).toHaveBeenCalledOnce()
  })
})
