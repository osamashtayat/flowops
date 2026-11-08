import { useId } from 'react'
import { Button } from './Button'
import { Select } from './Select'

export interface PaginationProps {
  /** 1-based. */
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  /** API limit is 1–100 (docs/API_CONTRACT.md §1). */
  pageSizeOptions?: readonly number[]
}

const DEFAULT_PAGE_SIZES = [10, 20, 50, 100] as const

/**
 * "Showing 21–40 of 132" + Previous/Next + page size. Controlled: keep `page` / `pageSize`
 * in the URL with `useUrlState` so a reload or shared link shows the same page.
 */
export function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = DEFAULT_PAGE_SIZES,
}: PaginationProps) {
  const sizeId = useId()
  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  const summary =
    totalCount === 0
      ? 'No results'
      : from > totalCount
        ? `This page is past the end (${totalCount} results)`
        : `Showing ${from}–${to} of ${totalCount}`

  return (
    <nav aria-label="Pagination" className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 text-sm">
      <p aria-live="polite" className="text-ink-muted tabular-nums">
        {summary}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor={sizeId} className="text-ink-muted">
          Rows per page
        </label>
        <div className="w-24">
          <Select
            id={sizeId}
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
          >
            {pageSizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </Select>
        </div>
        {/* Kept together so a narrow screen never wraps Next onto a line of its own. */}
        <div className="flex gap-3">
          <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
            Previous
          </Button>
          <Button variant="secondary" size="sm" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
            Next
          </Button>
        </div>
      </div>
    </nav>
  )
}
