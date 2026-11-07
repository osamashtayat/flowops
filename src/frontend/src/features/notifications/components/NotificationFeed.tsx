import { EmptyState } from '@/components/ui/EmptyState'
import { ErrorState } from '@/components/ui/ErrorState'
import { Skeleton } from '@/components/ui/Skeleton'
import { toUserMessage } from '@/lib/api/problem'
import type { PagedNotifications } from '@/lib/api/types'
import { NotificationList } from './NotificationList'

export interface NotificationFeedProps {
  /** The result of `useNotifications`. */
  query: { data: PagedNotifications | undefined; isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown }
  emptyMessage: string
  onOpen?: () => void
}

/** Loading, error, empty and list states of a notifications query; the page and the bell popover share it. */
export function NotificationFeed({ query, emptyMessage, onOpen }: NotificationFeedProps) {
  if (query.isPending) {
    return (
      <output aria-label="Loading notifications" className="block space-y-4 p-4">
        {[0, 1, 2].map((row) => (
          <div key={row} className="space-y-2">
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-3 w-full" />
          </div>
        ))}
      </output>
    )
  }
  if (query.isError) return <ErrorState message={toUserMessage(query.error)} onRetry={() => void query.refetch()} />
  if (!query.data || query.data.items.length === 0) return <EmptyState message={emptyMessage} />
  return <NotificationList items={query.data.items} onOpen={onOpen} />
}
