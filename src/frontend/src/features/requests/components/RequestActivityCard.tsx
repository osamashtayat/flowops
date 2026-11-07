import { Card } from '@/components/ui/Card'
import { ActivityTimeline } from '@/components/ui/ActivityTimeline'
import { Skeleton } from '@/components/ui/Skeleton'
import { ErrorState } from '@/components/ui/ErrorState'
import type { RequestType } from '@/lib/api/types'
import { toUserMessage } from '@/lib/api/problem'
import { useRequestActivity } from '../api'
import { CommentBox } from './CommentBox'

export interface RequestActivityCardProps {
  requestId: string
  requestType: RequestType
  canComment: boolean
}

export function RequestActivityCard({ requestId, requestType, canComment }: RequestActivityCardProps) {
  const activity = useRequestActivity(requestId)
  return (
    <Card title="Activity">
      <div className="space-y-5">
        {activity.isPending ? (
          <output aria-label="Loading activity" className="block space-y-3">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
          </output>
        ) : activity.isError ? (
          <ErrorState message={toUserMessage(activity.error)} onRetry={() => void activity.refetch()} />
        ) : (
          <ActivityTimeline items={activity.data} />
        )}
        {canComment && <CommentBox requestId={requestId} requestType={requestType} />}
      </div>
    </Card>
  )
}
