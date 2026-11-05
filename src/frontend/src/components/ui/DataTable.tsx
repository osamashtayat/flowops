import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { EmptyState } from './EmptyState'
import { ErrorState } from './ErrorState'
import { Skeleton } from './Skeleton'

export interface DataTableColumn<Row> {
  key: string
  header: string
  cell: (row: Row) => ReactNode
  /** Numbers and money: right-aligned with tabular figures. */
  align?: 'left' | 'right'
  /** Marks the cell that names the row (use for the column holding the row's link). */
  isRowHeader?: boolean
}

export interface DataTableProps<Row> {
  /** Accessible name of the table. */
  caption: string
  columns: readonly DataTableColumn<Row>[]
  rows: readonly Row[]
  getRowKey: (row: Row) => string
  isLoading?: boolean
  /** Shown instead of the table when set. */
  error?: { message: string; onRetry?: () => void }
  /** Shown when there are no rows. */
  empty?: ReactNode
  skeletonRows?: number
}

const cellBase = 'px-4 align-middle max-sm:block max-sm:px-4 max-sm:py-1'
const labelBefore = 'max-sm:before:mr-3 max-sm:before:text-xs max-sm:before:font-medium max-sm:before:text-ink-muted max-sm:before:content-[attr(data-label)]'

/**
 * Table with built-in loading (skeleton rows), empty and error states. Below 640 px each row
 * becomes a small card of label/value pairs. Make a row clickable with a real link in its
 * `isRowHeader` cell so keyboard and middle-click work.
 */
export function DataTable<Row>({
  caption,
  columns,
  rows,
  getRowKey,
  isLoading = false,
  error,
  empty = <EmptyState message="Nothing to show." />,
  skeletonRows = 5,
}: DataTableProps<Row>) {
  const shell = 'overflow-hidden rounded-md border border-border bg-surface shadow-card'

  if (error) {
    return (
      <div className={shell}>
        <ErrorState message={error.message} onRetry={error.onRetry} />
      </div>
    )
  }
  if (!isLoading && rows.length === 0) {
    return <div className={shell}>{empty}</div>
  }

  return (
    <div className={shell}>
      {isLoading && <output className="sr-only">Loading…</output>}
      <table aria-busy={isLoading || undefined} className="w-full border-collapse text-sm max-sm:block">
        <caption className="sr-only">{caption}</caption>
        <thead className="bg-surface-muted text-xs font-medium text-ink-muted max-sm:sr-only">
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className={cn('h-10 px-4 font-medium', column.align === 'right' ? 'text-right' : 'text-left')}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="max-sm:block">
          {isLoading
            ? Array.from({ length: skeletonRows }, (_, index) => (
                <tr key={index} aria-hidden="true" className="h-12 border-b border-border last:border-b-0 max-sm:block max-sm:h-auto max-sm:py-3">
                  {columns.map((column) => (
                    <td key={column.key} className={cellBase}>
                      <Skeleton className="h-4 w-3/4" />
                    </td>
                  ))}
                </tr>
              ))
            : rows.map((row) => (
                <tr
                  key={getRowKey(row)}
                  className="h-12 border-b border-border hover:bg-surface-muted last:border-b-0 max-sm:block max-sm:h-auto max-sm:py-3"
                >
                  {columns.map((column) => {
                    const Cell = column.isRowHeader ? 'th' : 'td'
                    return (
                      <Cell
                        key={column.key}
                        scope={column.isRowHeader ? 'row' : undefined}
                        data-label={column.header}
                        className={cn(
                          cellBase,
                          labelBefore,
                          'text-ink',
                          column.isRowHeader ? 'text-left font-medium' : 'font-normal',
                          column.align === 'right' && 'text-right tabular-nums max-sm:text-left',
                        )}
                      >
                        {column.cell(row)}
                      </Cell>
                    )
                  })}
                </tr>
              ))}
        </tbody>
      </table>
    </div>
  )
}
