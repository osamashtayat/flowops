import { Link } from 'react-router'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { DashboardDto, RequestStatus } from '@/lib/api/types'
import { requestStatusPresentation } from '@/lib/format/status'

type MySummary = DashboardDto['myRequests']

const TILES: { status: RequestStatus; count: (summary: MySummary) => number }[] = [
  { status: 'InReview', count: (s) => s.inReview },
  { status: 'Approved', count: (s) => s.approved },
  { status: 'Completed', count: (s) => s.completed },
  { status: 'Rejected', count: (s) => s.rejected },
]

/** The four counts of my requests; each opens the list filtered to that status. */
export function RequestStatTiles({ summary }: { summary: MySummary }) {
  return (
    <ul aria-label="My requests by status" className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {TILES.map(({ status, count }) => (
        <li key={status}>
          <Link
            to={`/requests?status=${status}`}
            className="flex h-full flex-col gap-3 rounded-md border border-border bg-surface p-5 shadow-card hover:bg-surface-muted"
          >
            <StatusBadge status={requestStatusPresentation(status)} className="self-start" />
            <span className="text-xl font-semibold tabular-nums text-ink">{count(summary)}</span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
