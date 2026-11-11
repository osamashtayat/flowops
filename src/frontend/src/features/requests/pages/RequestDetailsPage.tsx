import { useParams } from 'react-router'
import { PageHeader } from '@/components/layout/PageHeader'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { Card } from '@/components/ui/Card'
import { ErrorState } from '@/components/ui/ErrorState'
import { Skeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api/apiError'
import { toUserMessage } from '@/lib/api/problem'
import { useRequest } from '../api'
import { RequestActionsCard } from '../components/RequestActionsCard'
import { RequestActivityCard } from '../components/RequestActivityCard'
import { RequestDetailsCard } from '../components/RequestDetailsCard'
import { RequestHeader } from '../components/RequestHeader'
import { RequestSummaryCard } from '../components/RequestSummaryCard'
import { RouteCard } from '../components/RouteCard'

function LoadingSkeleton() {
  return (
    <output aria-label="Loading request" className="block space-y-6">
      <Skeleton className="h-8 w-1/2" />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Skeleton className="h-48 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
        <div className="space-y-6">
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      </div>
    </output>
  )
}

export function RequestDetailsPage() {
  const { id = '' } = useParams()
  const query = useRequest(id)

  if (query.isPending) return <LoadingSkeleton />

  if (query.isError) {
    // 404 covers both "no such request" and "you may not see it" — deliberately indistinguishable.
    if (query.error instanceof ApiError && query.error.status === 404) {
      return (
        <div className="space-y-6">
          <PageHeader title="Request not found" />
          <Card>
            <div className="space-y-4 text-center">
              <p className="text-base text-ink-muted">This request doesn&apos;t exist or you don&apos;t have access.</p>
              <ButtonLink to="/requests">Go to my requests</ButtonLink>
            </div>
          </Card>
        </div>
      )
    }
    return (
      <div className="space-y-6">
        <PageHeader title="Request" />
        <Card>
          <ErrorState message={toUserMessage(query.error)} onRetry={() => void query.refetch()} />
        </Card>
      </div>
    )
  }

  const request = query.data
  return (
    <div className="space-y-6">
      <RequestHeader request={request} />
      <div className="grid items-start gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <RequestDetailsCard request={request} />
          <RouteCard request={request} />
        </div>
        {/* Below lg the columns stack; the decider's action comes first instead of after the details (QA-03). */}
        <div className="max-lg:contents lg:space-y-6">
          <RequestSummaryCard request={request} />
          <div className="empty:hidden max-lg:order-first">
            <RequestActionsCard request={request} />
          </div>
          <RequestActivityCard requestId={request.id} requestType={request.type} canComment={request.canComment} />
        </div>
      </div>
    </div>
  )
}
