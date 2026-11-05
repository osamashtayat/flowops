import { Link } from 'react-router'
import { PageHeader } from '@/components/layout/PageHeader'

const TYPES = [
  { to: '/requests/new/purchase', title: 'Equipment purchase', description: 'Laptops, monitors, tools' },
  { to: '/requests/new/leave', title: 'Leave', description: 'Annual, sick, unpaid' },
] as const

/** Step one of a new request: choose what kind. Each card is a link, so keyboard and screen readers get it for free. */
export function NewRequestPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="New request" subtitle="What do you need?" />
      <ul className="grid gap-4 sm:grid-cols-2">
        {TYPES.map((type) => (
          <li key={type.to}>
            <Link
              to={type.to}
              className="block h-full rounded-md border border-border-strong bg-surface p-5 shadow-card hover:bg-primary-soft"
            >
              <span className="block text-lg font-semibold text-ink">{type.title}</span>
              <span className="mt-1 block text-base text-ink-muted">{type.description}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
