import { PageHeader } from '@/components/layout/PageHeader'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { useCapability } from '@/features/session/useCapability'
import { RequestsList } from '../components/RequestsList'

export function MyRequestsPage() {
  const canSubmit = useCapability('requests.submit')
  return (
    <div className="space-y-6">
      <PageHeader
        title="My requests"
        subtitle="Everything you have submitted."
        actions={canSubmit ? <ButtonLink to="/requests/new">New request</ButtonLink> : undefined}
      />
      <RequestsList scope="mine" caption="My requests" />
    </div>
  )
}
