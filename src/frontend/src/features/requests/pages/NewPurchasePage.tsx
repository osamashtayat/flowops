import { PageHeader } from '@/components/layout/PageHeader'
import { Card } from '@/components/ui/Card'
import { PurchaseForm } from '../components/PurchaseForm'

export function NewPurchasePage() {
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader title="Equipment purchase" subtitle="Laptops, monitors, tools. Your manager and finance will review it." />
      <Card>
        <PurchaseForm />
      </Card>
    </div>
  )
}
