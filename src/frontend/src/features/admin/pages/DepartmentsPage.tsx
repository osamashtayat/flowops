import { useState } from 'react'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/Button'
import { Checkbox } from '@/components/ui/Checkbox'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { toUserMessage } from '@/lib/api/problem'
import type { DepartmentDto } from '@/lib/api/types'
import { activeStatusPresentation } from '@/lib/format/status'
import { useDepartments } from '../api'
import { DepartmentDialog } from '../components/DepartmentDialog'

/** `undefined` = closed, `null` = creating, a department = editing it. */
type DialogTarget = DepartmentDto | null | undefined

export function DepartmentsPage() {
  const [showInactive, setShowInactive] = useState(true)
  const [target, setTarget] = useState<DialogTarget>(undefined)
  const query = useDepartments(showInactive)

  const columns: DataTableColumn<DepartmentDto>[] = [
    { key: 'name', header: 'Name', isRowHeader: true, cell: (d) => d.name },
    { key: 'code', header: 'Code', cell: (d) => <span className="tabular-nums">{d.code}</span> },
    { key: 'members', header: 'Active members', align: 'right', cell: (d) => d.memberCount },
    { key: 'status', header: 'Status', cell: (d) => <StatusBadge status={activeStatusPresentation(d.isActive)} /> },
    {
      key: 'actions',
      header: 'Actions',
      cell: (d) => (
        <Button variant="ghost" size="sm" aria-label={`Edit ${d.name}`} onClick={() => setTarget(d)}>
          Edit
        </Button>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Departments"
        subtitle="Every request belongs to its requester's department."
        actions={<Button onClick={() => setTarget(null)}>New department</Button>}
      />
      <Checkbox label="Show inactive" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
      <DataTable
        caption="Departments"
        columns={columns}
        rows={query.data ?? []}
        getRowKey={(d) => d.id}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={<EmptyState message="There are no departments yet." action={<Button onClick={() => setTarget(null)}>New department</Button>} />}
      />
      <DepartmentDialog key={target?.id ?? 'new'} open={target !== undefined} department={target ?? undefined} onClose={() => setTarget(undefined)} />
    </div>
  )
}
