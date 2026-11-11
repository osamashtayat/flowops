import { PageHeader } from '@/components/layout/PageHeader'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { Pagination } from '@/components/ui/Pagination'
import { REQUEST_TYPE_LABELS, ROLE_NAMES } from '@/lib/format/labels'
import { toUserMessage } from '@/lib/api/problem'
import type { StalledTaskDto } from '@/lib/api/types'
import { formatRelative } from '@/lib/format/date'
import { stepLabel } from '@/lib/format/status'
import { useUrlState } from '@/lib/useUrlState'
import { useStalledTasks } from '../api'
import { STALLED_REASONS } from '../labels'

const URL_DEFAULTS = { page: 1, pageSize: 20 }

function assigneeName(task: StalledTaskDto): string {
  return task.assignee.user?.displayName ?? (task.assignee.role ? ROLE_NAMES[task.assignee.role] : '—')
}

const columns: DataTableColumn<StalledTaskDto>[] = [
  { key: 'number', header: 'Request', isRowHeader: true, cell: (t) => <span className="tabular-nums">{t.requestNumber}</span> },
  { key: 'type', header: 'Type', cell: (t) => REQUEST_TYPE_LABELS[t.requestType] },
  { key: 'stage', header: 'Stage', cell: (t) => stepLabel(t.step) },
  { key: 'assignee', header: 'Assignee', cell: assigneeName },
  {
    key: 'reason',
    header: 'Reason',
    cell: (t) => (
      <div>
        <p>{STALLED_REASONS[t.reason].label}</p>
        <p className="text-xs text-ink-muted">{STALLED_REASONS[t.reason].guidance(t.assignee.role)}</p>
      </div>
    ),
  },
  { key: 'since', header: 'Open since', cell: (t) => formatRelative(t.openSince) },
]

/** Open tasks nobody can currently decide. Admins see metadata only, so there is no link to the request. */
export function StalledTasksPage() {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  const query = useStalledTasks({ page: url.page, pageSize: url.pageSize })
  return (
    <div className="space-y-6">
      <PageHeader title="Stalled tasks" subtitle="Open approval or fulfilment steps that nobody can currently act on." />
      <DataTable
        caption="Stalled tasks"
        columns={columns}
        rows={query.data?.items ?? []}
        getRowKey={(t) => t.taskId}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={<EmptyState message="No stalled tasks. Every open step has someone who can act on it." />}
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
