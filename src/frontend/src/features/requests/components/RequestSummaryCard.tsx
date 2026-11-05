import { Card } from '@/components/ui/Card'
import type { RequestDetailsDto } from '@/lib/api/types'
import { formatDate } from '@/lib/format/date'

export function RequestSummaryCard({ request }: { request: RequestDetailsDto }) {
  const rows: [string, string][] = [
    ['Requester', request.requester.displayName],
    ['Department', request.department.name],
    ['Submitted', formatDate(request.submittedAt)],
    ['Closed', request.closedAt ? formatDate(request.closedAt) : '—'],
  ]
  return (
    <Card title="Summary">
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-ink-muted">{label}</dt>
            <dd className="text-right text-ink tabular-nums">{value}</dd>
          </div>
        ))}
      </dl>
    </Card>
  )
}
