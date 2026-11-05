import { PageHeader } from '@/components/layout/PageHeader'
import { Card } from '@/components/ui/Card'
import { LeaveForm } from '../components/LeaveForm'

export function NewLeavePage() {
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader title="Leave" subtitle="Annual, sick, unpaid. Your manager and HR will review it." />
      <Card>
        <LeaveForm />
      </Card>
    </div>
  )
}
