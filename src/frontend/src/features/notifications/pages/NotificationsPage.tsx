import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/Button'
import { Checkbox } from '@/components/ui/Checkbox'
import { Pagination } from '@/components/ui/Pagination'
import { useToast } from '@/components/ui/toastContext'
import { toUserMessage } from '@/lib/api/problem'
import { useUrlState } from '@/lib/useUrlState'
import { useMarkAllNotificationsRead, useNotifications, useUnreadCount } from '../api'
import { NotificationFeed } from '../components/NotificationFeed'

const URL_DEFAULTS = { unread: '', page: 1, pageSize: 20 }

export function NotificationsPage() {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  const unreadOnly = url.unread === '1'
  const query = useNotifications({ unreadOnly, page: url.page, pageSize: url.pageSize })
  const unread = useUnreadCount()
  const markAll = useMarkAllNotificationsRead()
  const toast = useToast()

  const onMarkAll = () =>
    markAll.mutate(undefined, {
      onSuccess: () => toast.success('All notifications marked as read.'),
      onError: (error) => toast.error(toUserMessage(error)),
    })

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notifications"
        subtitle="What happened on your requests and tasks."
        actions={
          <Button variant="secondary" onClick={onMarkAll} loading={markAll.isPending} disabled={unread.data === 0}>
            Mark all as read
          </Button>
        }
      />
      <Checkbox
        label="Unread only"
        checked={unreadOnly}
        onChange={(event) => setUrl({ unread: event.target.checked ? '1' : '' })}
      />
      <div className="rounded-md border border-border bg-surface shadow-card">
        <NotificationFeed
          query={query}
          emptyMessage={
            query.data && query.data.totalCount > 0
              ? 'This page is past the end of the list.'
              : unreadOnly
                ? "You're all caught up."
                : 'You have no notifications yet.'
          }
        />
      </div>
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
