import { PageHeader } from '@/components/layout/PageHeader'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { FilterBar } from '@/components/ui/FilterBar'
import { FormField } from '@/components/ui/FormField'
import { Pagination } from '@/components/ui/Pagination'
import { Select } from '@/components/ui/Select'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { RoleChangeDto, RoleChangeStatus } from '@/lib/api/types'
import { toUserMessage } from '@/lib/api/problem'
import { formatDateTime, formatRelative } from '@/lib/format/date'
import { roleChangeStatusPresentation } from '@/lib/format/status'
import { useUrlState } from '@/lib/useUrlState'
import { useRoleChanges } from '../api'
import { RoleChangeActions } from '../components/RoleChangeActions'
import { rolesText } from '../labels'

const STATUSES: readonly RoleChangeStatus[] = ['Pending', 'Approved', 'Rejected', 'Cancelled', 'Expired']
/** "all" is the URL spelling of "no status filter", so that Pending can be the default. */
const URL_DEFAULTS = { page: 1, pageSize: 20, status: 'Pending' }

function decision(change: RoleChangeDto) {
  if (change.status === 'Pending') {
    // The server offers `cancel` only to the proposer, so it is also what tells us this one is ours.
    return change.availableActions.includes('cancel') ? 'Waiting for another administrator' : `Expires ${formatRelative(change.expiresAt)}`
  }
  const by = change.decidedBy?.displayName
  const at = change.decidedAt ? formatDateTime(change.decidedAt) : ''
  return [by && `${change.status} by ${by}`, at].filter(Boolean).join(' · ') || change.status
}

const columns: DataTableColumn<RoleChangeDto>[] = [
  { key: 'person', header: 'Administrator', isRowHeader: true, cell: (c) => c.user.displayName },
  {
    key: 'change',
    header: 'Roles',
    cell: (c) => (
      <div>
        <p>{rolesText(c.proposedRoles)}</p>
        <p className="text-xs text-ink-muted">Now: {rolesText(c.previousRoles)}</p>
      </div>
    ),
  },
  { key: 'proposed', header: 'Proposed', cell: (c) => formatDateTime(c.proposedAt) },
  {
    key: 'status',
    header: 'Status',
    cell: (c) => (
      <div className="space-y-1">
        <StatusBadge status={roleChangeStatusPresentation(c.status)} />
        <p className="text-xs text-ink-muted">{decision(c)}</p>
        {c.rejectionReason && <p className="text-xs text-ink-muted">Reason: {c.rejectionReason}</p>}
      </div>
    ),
  },
  { key: 'actions', header: 'Actions', cell: (c) => <RoleChangeActions change={c} /> },
]

/** An administrator's own role changes wait here for another administrator (ADR-0012, docs/UI_SPEC.md §4.8). */
export function RoleChangesPage() {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  const status = STATUSES.find((s) => s === url.status) ?? ''
  const query = useRoleChanges({ status, page: url.page, pageSize: url.pageSize })

  return (
    <div className="space-y-6">
      <PageHeader title="Role changes" subtitle="An administrator can't change their own roles alone. Another administrator approves or rejects the change within 72 hours." />
      <FilterBar label="Filter role changes">
        <FormField label="Status">
          {(c) => (
            <Select {...c} value={status || 'all'} onChange={(e) => setUrl({ status: e.target.value, page: 1 })}>
              <option value="all">All statuses</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {roleChangeStatusPresentation(s).label}
                </option>
              ))}
            </Select>
          )}
        </FormField>
      </FilterBar>
      <DataTable
        caption="Role changes"
        columns={columns}
        rows={query.data?.items ?? []}
        getRowKey={(c) => c.id}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={<EmptyState message={status === 'Pending' ? 'No role changes are waiting for approval.' : 'No role changes match this filter.'} />}
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
