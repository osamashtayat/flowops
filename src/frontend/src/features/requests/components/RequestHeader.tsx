import { PageHeader } from '@/components/layout/PageHeader'
import { StatusBadge } from '@/components/ui/StatusBadge'
import type { RequestDetailsDto } from '@/lib/api/types'
import { requestStageLabel, requestStatusPresentation } from '@/lib/format/status'

/** "FO-2026-000123 · Dell XPS 15", the status, and — while it is open — where it waits. */
export function RequestHeader({ request }: { request: RequestDetailsDto }) {
  return (
    <PageHeader
      title={`${request.number} · ${request.title}`}
      subtitle={request.status === 'InReview' ? `Waiting for: ${requestStageLabel(request.stage)}` : undefined}
      actions={<StatusBadge status={requestStatusPresentation(request.status)} />}
    />
  )
}
