import { useState } from 'react'
import { Card } from '@/components/ui/Card'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { FilterBar } from '@/components/ui/FilterBar'
import { FormField } from '@/components/ui/FormField'
import { Pagination } from '@/components/ui/Pagination'
import { Select } from '@/components/ui/Select'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { formatDate } from '@/lib/format/date'
import { formatMoney, type Money } from '@/lib/format/money'
import { requestStatusPresentation, type RequestStatus } from '@/lib/format/status'
import { useUrlState } from '@/lib/useUrlState'

interface DemoRow {
  id: string
  number: string
  status: RequestStatus
  cost: Money
  submittedAt: string
}

const statusOrder: RequestStatus[] = ['InReview', 'Approved', 'Completed', 'Rejected']
const allRows: DemoRow[] = Array.from({ length: 47 }, (_, i) => ({
  id: String(i + 1),
  number: `FO-2026-${String(i + 1).padStart(6, '0')}`,
  status: statusOrder[i % 4] ?? 'InReview',
  cost: { amount: `${(i + 1) * 137}.${String((i * 37) % 1000).padStart(3, '0')}`, currency: 'JOD' },
  submittedAt: new Date(Date.UTC(2026, 9, 1 + (i % 28), 9 + (i % 12))).toISOString(),
}))

const columns: DataTableColumn<DemoRow>[] = [
  { key: 'number', header: 'Request', isRowHeader: true, cell: (r) => <a className="text-primary underline" href={`#${r.id}`}>{r.number}</a> },
  { key: 'status', header: 'Status', cell: (r) => <StatusBadge status={requestStatusPresentation(r.status)} /> },
  { key: 'submitted', header: 'Submitted', cell: (r) => <span className="tabular-nums">{formatDate(r.submittedAt)}</span> },
  { key: 'cost', header: 'Estimated cost', align: 'right', cell: (r) => formatMoney(r.cost) },
]

const urlDefaults = { page: 1, pageSize: 10, status: '' }
type Mode = 'data' | 'loading' | 'empty' | 'error'

/** Review aid for FE-05: table states, URL-backed filter and paging. Removed with FoundationPage. */
export function DataDisplayDemo() {
  const [url, setUrl] = useUrlState(urlDefaults)
  const [mode, setMode] = useState<Mode>('data')

  const filtered = allRows.filter((r) => url.status === '' || r.status === url.status)
  const start = (url.page - 1) * url.pageSize
  const pageRows = mode === 'empty' ? [] : filtered.slice(start, start + url.pageSize)

  return (
    <Card title="Data display: table, filters, pagination (state lives in the URL)">
      <div className="space-y-4">
        <FilterBar hasActiveFilters={url.status !== ''} onClear={() => setUrl({ status: '' })}>
          <FormField label="Status">
            {(c) => (
              <Select {...c} value={url.status} onChange={(e) => setUrl({ status: e.target.value })} placeholder="All statuses">
                {statusOrder.map((s) => (
                  <option key={s} value={s}>
                    {requestStatusPresentation(s).label}
                  </option>
                ))}
              </Select>
            )}
          </FormField>
          <FormField label="Demo state">
            {(c) => (
              <Select {...c} value={mode} onChange={(e) => setMode(e.target.value as Mode)}>
                <option value="data">Data</option>
                <option value="loading">Loading</option>
                <option value="empty">Empty</option>
                <option value="error">Error</option>
              </Select>
            )}
          </FormField>
        </FilterBar>

        <DataTable
          caption="Demo requests"
          columns={columns}
          rows={pageRows}
          getRowKey={(r) => r.id}
          isLoading={mode === 'loading'}
          error={mode === 'error' ? { message: 'Could not load requests. Check your connection and try again.', onRetry: () => setMode('data') } : undefined}
          empty={<EmptyState message="You have no requests yet." />}
        />

        <Pagination
          page={url.page}
          pageSize={url.pageSize}
          totalCount={mode === 'empty' ? 0 : filtered.length}
          onPageChange={(page) => setUrl({ page })}
          onPageSizeChange={(pageSize) => setUrl({ pageSize })}
        />
      </div>
    </Card>
  )
}
