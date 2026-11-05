import { Link } from 'react-router'
import { Card } from '@/components/ui/Card'
import type { DashboardDto } from '@/lib/api/types'

type AdminSummary = NonNullable<DashboardDto['admin']>

/** Organisation at a glance for administrators. Stalled tasks get the warning tone when there are any. */
export function AdminCard({ summary }: { summary: AdminSummary }) {
  const items = [
    { to: '/admin/users', label: 'Active users', value: summary.activeUsers, warn: false },
    { to: '/admin/departments', label: 'Departments', value: summary.departments, warn: false },
    { to: '/admin/stalled-tasks', label: 'Stalled tasks', value: summary.stalledTasks, warn: summary.stalledTasks > 0 },
  ]
  return (
    <Card title="Administration">
      <ul className="grid gap-4 sm:grid-cols-3">
        {items.map((item) => (
          <li key={item.to}>
            <Link
              to={item.to}
              className={`flex h-full flex-col gap-1 rounded-md border p-4 hover:bg-surface-muted ${item.warn ? 'border-warning bg-warning-soft' : 'border-border bg-surface'}`}
            >
              <span className="text-sm text-ink-muted">{item.label}</span>
              <span className={`text-xl font-semibold tabular-nums ${item.warn ? 'text-warning' : 'text-ink'}`}>{item.value}</span>
              {item.warn && <span className="text-xs text-warning">Needs attention</span>}
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  )
}
