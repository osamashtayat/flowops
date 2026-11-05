import { PageHeader } from '@/components/layout/PageHeader'
import { ActivityTimeline } from '@/components/ui/ActivityTimeline'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { Card } from '@/components/ui/Card'
import { ErrorState } from '@/components/ui/ErrorState'
import { Skeleton } from '@/components/ui/Skeleton'
import { useCapability } from '@/features/session/useCapability'
import { useSession } from '@/features/session/api'
import { toUserMessage } from '@/lib/api/problem'
import { useDashboard } from '../api'
import { AdminCard } from '../components/AdminCard'
import { RecentRequestsCard } from '../components/RecentRequestsCard'
import { RequestStatTiles } from '../components/RequestStatTiles'
import { TasksCard } from '../components/TasksCard'

function LoadingSkeleton() {
  return (
    <output aria-label="Loading dashboard" className="block space-y-6">
      <Skeleton className="h-24 w-full" />
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[0, 1, 2, 3].map((tile) => (
          <Skeleton key={tile} className="h-24 w-full" />
        ))}
      </div>
      <Skeleton className="h-56 w-full" />
    </output>
  )
}

/** Order is what needs attention first (docs/UI_SPEC.md §4.2). Sections the server left null are not shown. */
export function DashboardPage() {
  const { user } = useSession()
  const canSubmit = useCapability('requests.submit')
  const query = useDashboard()
  const header = (
    <PageHeader
      title="Dashboard"
      subtitle={user ? `Welcome, ${user.displayName}.` : undefined}
      actions={canSubmit ? <ButtonLink to="/requests/new">New request</ButtonLink> : undefined}
    />
  )

  if (query.isPending) {
    return (
      <div className="space-y-6">
        {header}
        <LoadingSkeleton />
      </div>
    )
  }
  if (query.isError) {
    return (
      <div className="space-y-6">
        {header}
        <Card>
          <ErrorState message={toUserMessage(query.error)} onRetry={() => void query.refetch()} />
        </Card>
      </div>
    )
  }

  const { myTasks, myRequests, recentActivity, admin } = query.data
  // People who cannot submit (administrators) have no requests of their own to summarise.
  const showMine = canSubmit || myRequests.recent.length > 0
  return (
    <div className="space-y-6">
      {header}
      {myTasks && <TasksCard tasks={myTasks} />}
      {showMine && (
        <>
          <RequestStatTiles summary={myRequests} />
          <RecentRequestsCard requests={myRequests.recent} />
        </>
      )}
      {recentActivity.length > 0 && (
        <Card title="Recent activity">
          <ActivityTimeline items={recentActivity} />
        </Card>
      )}
      {admin && <AdminCard summary={admin} />}
    </div>
  )
}
