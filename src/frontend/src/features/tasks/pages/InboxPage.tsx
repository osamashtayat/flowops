import { PageHeader } from '@/components/layout/PageHeader'
import { useUrlState } from '@/lib/useUrlState'
import { InboxList } from '../components/InboxList'
import { InboxTabs, type InboxView } from '../components/InboxTabs'

const URL_DEFAULTS = { status: 'Open', page: 1, pageSize: 20 }

export function InboxPage() {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  // A URL can say anything; only the two known views are used.
  const view: InboxView = url.status === 'Done' ? 'Done' : 'Open'
  return (
    <div className="space-y-6">
      <PageHeader title="Inbox" subtitle="Requests waiting for your decision, and the ones you have decided." />
      <InboxTabs value={view} onChange={(next) => setUrl({ status: next })} />
      <InboxList
        view={view}
        page={url.page}
        pageSize={url.pageSize}
        onPageChange={(page) => setUrl({ page })}
        onPageSizeChange={(pageSize) => setUrl({ pageSize })}
      />
    </div>
  )
}
