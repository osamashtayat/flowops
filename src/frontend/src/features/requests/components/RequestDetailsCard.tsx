import type { ReactNode } from 'react'
import { Card } from '@/components/ui/Card'
import type { RequestDetailsDto } from '@/lib/api/types'
import { formatDateOnly } from '@/lib/format/date'
import { formatMoney } from '@/lib/format/money'
import { LEAVE_TYPE_LABELS } from '@/lib/format/labels'

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-sm font-medium text-ink-muted">{label}</dt>
      <dd className="mt-0.5 whitespace-pre-wrap break-words text-base text-ink">{children}</dd>
    </div>
  )
}

/** What was asked for. Which block shows depends on the request's own details, not on the user's role. */
export function RequestDetailsCard({ request }: { request: RequestDetailsDto }) {
  const { purchase, leave } = request
  return (
    <Card title="Details">
      <dl className="grid gap-4">
        {purchase && (
          <>
            <Fact label="Item">{purchase.itemName}</Fact>
            <Fact label="Description">{purchase.description}</Fact>
            <Fact label="Estimated cost">
              <span className="tabular-nums">{formatMoney(purchase.estimatedCost)}</span>
            </Fact>
            <Fact label="Business justification">{purchase.justification}</Fact>
          </>
        )}
        {leave && (
          <>
            <Fact label="Leave type">{LEAVE_TYPE_LABELS[leave.leaveType]}</Fact>
            <Fact label="Dates">
              <span className="tabular-nums">
                {leave.startDate === leave.endDate
                  ? formatDateOnly(leave.startDate)
                  : `${formatDateOnly(leave.startDate)} – ${formatDateOnly(leave.endDate)}`}
              </span>{' '}
              <span className="text-ink-muted">
                ({leave.days} calendar {leave.days === 1 ? 'day' : 'days'})
              </span>
            </Fact>
            <Fact label="Reason">
              {leave.reasonHidden ? (
                <span className="text-ink-muted">Restricted. Only the requester, their manager and HR can read it.</span>
              ) : (
                (leave.reason ?? <span className="text-ink-muted">No reason given.</span>)
              )}
            </Fact>
          </>
        )}
      </dl>
    </Card>
  )
}
