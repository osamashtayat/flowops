import { useCallback } from 'react'
import { Link } from 'react-router'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { Button } from '@/components/ui/Button'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { Pagination } from '@/components/ui/Pagination'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { useCapability } from '@/features/session/useCapability'
import { toUserMessage } from '@/lib/api/problem'
import type { RequestScope, RequestStatus, RequestSummaryDto, RequestType } from '@/lib/api/types'
import { formatDate } from '@/lib/format/date'
import { requestStageLabel, requestStatusPresentation } from '@/lib/format/status'
import { useUrlState } from '@/lib/useUrlState'
import { useRequests } from '../api'
import { REQUEST_LIMITS } from '../limits'
import { REQUEST_STATUSES, REQUEST_TYPES, REQUEST_TYPE_LABELS } from '../labels'
import { RequestFilters, type RequestFilterValues } from './RequestFilters'

const URL_DEFAULTS = { page: 1, pageSize: 20, status: '', type: '', search: '' }

/** A URL can say anything; only values the contract knows go to the API. */
function oneOf<T extends string>(allowed: readonly T[], value: string): T | '' {
  return allowed.find((candidate) => candidate === value) ?? ''
}

const baseColumns: DataTableColumn<RequestSummaryDto>[] = [
  { key: 'number', header: 'Number', cell: (r) => <span className="tabular-nums">{r.number}</span> },
  {
    key: 'title',
    header: 'Title',
    isRowHeader: true,
    cell: (r) => (
      <Link to={`/requests/${r.id}`} className="text-primary underline">
        {r.title}
      </Link>
    ),
  },
  { key: 'type', header: 'Type', cell: (r) => REQUEST_TYPE_LABELS[r.type] },
  { key: 'status', header: 'Status', cell: (r) => <StatusBadge status={requestStatusPresentation(r.status)} /> },
  { key: 'stage', header: 'Stage', cell: (r) => requestStageLabel(r.stage) },
  { key: 'submitted', header: 'Submitted', cell: (r) => <span className="tabular-nums">{formatDate(r.submittedAt)}</span> },
]

const requesterColumn: DataTableColumn<RequestSummaryDto> = {
  key: 'requester',
  header: 'Requester',
  cell: (r) => r.requester.displayName,
}

export interface RequestsListProps {
  /** `mine` for My requests, `visible` for Team requests (docs/API_CONTRACT.md §4). */
  scope: RequestScope
  caption: string
}

/** Filters + table + pagination over `GET /api/requests`, with filters and page kept in the URL. */
export function RequestsList({ scope, caption }: RequestsListProps) {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  const canSubmit = useCapability('requests.submit')
  const filters: RequestFilterValues = {
    status: oneOf<RequestStatus>(REQUEST_STATUSES, url.status),
    type: oneOf<RequestType>(REQUEST_TYPES, url.type),
    search: url.search.slice(0, REQUEST_LIMITS.searchMax),
  }
  const query = useRequests({ scope, ...filters, page: url.page, pageSize: url.pageSize })
  const hasFilters = filters.status !== '' || filters.type !== '' || filters.search !== ''
  const clear = useCallback(() => setUrl({ status: '', type: '', search: '' }), [setUrl])
  const change = useCallback((patch: Partial<RequestFilterValues>) => setUrl(patch), [setUrl])

  const pastTheEnd = (query.data?.totalCount ?? 0) > 0 && url.page > 1
  const empty = pastTheEnd ? (
    <EmptyState message="This page is past the end of the list." action={<Button variant="secondary" onClick={() => setUrl({ page: 1 })}>Go to the first page</Button>} />
  ) : hasFilters ? (
    <EmptyState message="No requests match these filters." action={<Button variant="secondary" onClick={clear}>Clear filters</Button>} />
  ) : scope === 'mine' ? (
    <EmptyState
      message="You have no requests yet."
      action={canSubmit ? <ButtonLink to="/requests/new">New request</ButtonLink> : undefined}
    />
  ) : (
    <EmptyState message="There are no requests to show yet." />
  )

  return (
    <div className="space-y-4">
      <RequestFilters values={filters} onChange={change} onClear={clear} />
      <DataTable
        caption={caption}
        columns={scope === 'visible' ? [...baseColumns, requesterColumn] : baseColumns}
        rows={query.data?.items ?? []}
        getRowKey={(r) => r.id}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={empty}
      />
      <Pagination
        page={url.page}
        pageSize={url.pageSize}
        totalCount={query.data?.totalCount ?? 0}
        onPageChange={(page) => setUrl({ page })}
        onPageSizeChange={(pageSize) => setUrl({ pageSize })}
      />
    </div>
  )
}
