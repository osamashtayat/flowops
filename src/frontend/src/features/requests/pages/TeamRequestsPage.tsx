import { PageHeader } from '@/components/layout/PageHeader'
import { RequestsList } from '../components/RequestsList'

export function TeamRequestsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Team requests" subtitle="Requests you are allowed to see." />
      <RequestsList scope="visible" caption="Team requests" />
    </div>
  )
}
