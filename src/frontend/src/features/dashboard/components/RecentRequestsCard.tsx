import { Link } from 'react-router'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { Card } from '@/components/ui/Card'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { useCapability } from '@/features/session/useCapability'
import type { RequestSummaryDto } from '@/lib/api/types'
import { formatDate } from '@/lib/format/date'
import { requestStatusPresentation } from '@/lib/format/status'

const COLUMNS: DataTableColumn<RequestSummaryDto>[] = [
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
  { key: 'status', header: 'Status', cell: (r) => <StatusBadge status={requestStatusPresentation(r.status)} /> },
  { key: 'submitted', header: 'Submitted', cell: (r) => <span className="tabular-nums">{formatDate(r.submittedAt)}</span> },
]

export function RecentRequestsCard({ requests }: { requests: readonly RequestSummaryDto[] }) {
  const canSubmit = useCapability('requests.submit')
  return (
    <Card title="Recent requests" actions={<ButtonLink to="/requests" variant="secondary">View all</ButtonLink>}>
      <DataTable
        caption="Recent requests"
        columns={COLUMNS}
        rows={requests}
        getRowKey={(r) => r.id}
        empty={
          <EmptyState
            message="You have no requests yet."
            action={canSubmit ? <ButtonLink to="/requests/new">New request</ButtonLink> : undefined}
          />
        }
      />
    </Card>
  )
}
