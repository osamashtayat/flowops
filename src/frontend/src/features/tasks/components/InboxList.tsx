import { Link } from 'react-router'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { Pagination } from '@/components/ui/Pagination'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { toUserMessage } from '@/lib/api/problem'
import type { InboxItemDto } from '@/lib/api/types'
import { formatDate, formatRelative } from '@/lib/format/date'
import { requestStageLabel, taskStatusPresentation } from '@/lib/format/status'
import { useInbox } from '../api'
import { DecisionActions } from './DecisionActions'
import type { InboxView } from './InboxTabs'

const requestColumn: DataTableColumn<InboxItemDto> = {
  key: 'request',
  header: 'Request',
  isRowHeader: true,
  cell: (item) => (
    <div>
      <Link to={`/requests/${item.request.id}`} className="text-primary underline">
        {item.request.title}
      </Link>
      <p className="text-xs text-ink-muted tabular-nums">{item.request.number}</p>
    </div>
  ),
}

const sharedColumns: DataTableColumn<InboxItemDto>[] = [
  requestColumn,
  {
    key: 'requester',
    header: 'Requester',
    // One column for both keeps the table, with its action buttons, inside 1024 px.
    cell: (item) => (
      <div>
        <p>{item.request.requester.displayName}</p>
        <p className="text-xs text-ink-muted">{item.request.department.name}</p>
      </div>
    ),
  },
  { key: 'detail', header: 'Detail', cell: (item) => <span className="tabular-nums">{item.request.keyDetail}</span> },
  { key: 'stage', header: 'Stage', cell: (item) => requestStageLabel(item.stage) },
]

const openColumns: DataTableColumn<InboxItemDto>[] = [
  ...sharedColumns,
  { key: 'age', header: 'Waiting', cell: (item) => formatRelative(item.createdAt) },
  {
    key: 'actions',
    header: 'Actions',
    cell: (item) => (
      <DecisionActions
        taskId={item.taskId}
        version={item.version}
        stage={item.stage}
        requestNumber={item.request.number}
        actions={item.actions}
      />
    ),
  },
]

const doneColumns: DataTableColumn<InboxItemDto>[] = [
  ...sharedColumns,
  { key: 'outcome', header: 'Outcome', cell: (item) => <StatusBadge status={taskStatusPresentation(item.outcome)} /> },
  { key: 'decided', header: 'Decided', cell: (item) => <span className="tabular-nums">{item.decidedAt ? formatDate(item.decidedAt) : '—'}</span> },
]

export interface InboxListProps {
  view: InboxView
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

/** The task list for one tab. After a decision the shared mutation refetches `tasks`, so the item leaves Open by itself. */
export function InboxList({ view, page, pageSize, onPageChange, onPageSizeChange }: InboxListProps) {
  const query = useInbox({ status: view, page, pageSize })
  const total = query.data?.totalCount ?? 0
  const empty =
    total > 0 && page > 1 ? (
      <EmptyState message="This page is past the end of the list." />
    ) : view === 'Open' ? (
      <EmptyState message="You're all caught up." />
    ) : (
      <EmptyState message="You haven't decided any tasks yet." />
    )

  return (
    <div className="space-y-4">
      <DataTable
        caption={view === 'Open' ? 'Open tasks' : 'Completed tasks'}
        columns={view === 'Open' ? openColumns : doneColumns}
        rows={query.data?.items ?? []}
        getRowKey={(item) => item.taskId}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={empty}
      />
      <Pagination page={page} pageSize={pageSize} totalCount={total} onPageChange={onPageChange} onPageSizeChange={onPageSizeChange} />
    </div>
  )
}
