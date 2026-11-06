import { useCallback } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/Button'
import { Checkbox } from '@/components/ui/Checkbox'
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable'
import { EmptyState } from '@/components/ui/EmptyState'
import { FilterBar } from '@/components/ui/FilterBar'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { Pagination } from '@/components/ui/Pagination'
import { Select } from '@/components/ui/Select'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { activeStatusPresentation } from '@/lib/format/status'
import { toUserMessage } from '@/lib/api/problem'
import type { Role, UserDto } from '@/lib/api/types'
import { useDebouncedUrlSearch } from '@/lib/useDebouncedUrlSearch'
import { useUrlState } from '@/lib/useUrlState'
import { useDepartments, useUsers } from '../api'
import { ROLES, ROLE_NAMES } from '../labels'

const URL_DEFAULTS = { page: 1, pageSize: 20, search: '', department: '', role: '', inactive: '' }
const SEARCH_MAX = 100

const columns: DataTableColumn<UserDto>[] = [
  {
    key: 'name',
    header: 'Name',
    isRowHeader: true,
    cell: (u) => (
      <Link to={`/admin/users/${u.id}`} className="text-primary underline">
        {u.displayName}
      </Link>
    ),
  },
  { key: 'email', header: 'Email', cell: (u) => u.email },
  { key: 'department', header: 'Department', cell: (u) => u.department?.name ?? '—' },
  { key: 'manager', header: 'Manager', cell: (u) => u.manager?.displayName ?? '—' },
  {
    key: 'roles',
    header: 'Roles',
    cell: (u) => (
      <ul className="flex flex-wrap gap-1">
        {u.roles.map((role) => (
          <li key={role} className="rounded-sm bg-surface-muted px-2 py-0.5 text-xs text-ink">
            {ROLE_NAMES[role]}
          </li>
        ))}
      </ul>
    ),
  },
  {
    key: 'status',
    header: 'Status',
    cell: (u) => <StatusBadge status={activeStatusPresentation(u.isActive)} />,
  },
]

/** Search + department + role filters, an "Include inactive" switch, the table and pagination over `GET /api/users`. */
export function UsersList() {
  const [url, setUrl] = useUrlState(URL_DEFAULTS)
  const departments = useDepartments(true)
  const role: Role | '' = ROLES.find((candidate) => candidate === url.role) ?? ''
  const departmentId = departments.data?.some((d) => d.id === url.department) ? url.department : ''
  const filters = { search: url.search.slice(0, SEARCH_MAX), departmentId, role, includeInactive: url.inactive === 'true' }
  const query = useUsers({ ...filters, page: url.page, pageSize: url.pageSize })
  const hasFilters = filters.search !== '' || departmentId !== '' || role !== '' || filters.includeInactive
  const clear = useCallback(() => setUrl({ search: '', department: '', role: '', inactive: '' }), [setUrl])
  const search = useDebouncedUrlSearch(filters.search, (value) => setUrl({ search: value }))

  const empty = hasFilters ? (
    <EmptyState message="No users match these filters." action={<Button variant="secondary" onClick={clear}>Clear filters</Button>} />
  ) : (
    <EmptyState message="There are no users yet." />
  )

  return (
    <div className="space-y-4">
      <FilterBar label="Filter users" hasActiveFilters={hasFilters} onClear={clear}>
        <FormField label="Search" helperText="Name or email.">
          {(c) => <Input {...c} type="search" value={search.value} maxLength={SEARCH_MAX} onChange={(e) => search.onChange(e.target.value)} />}
        </FormField>
        <FormField label="Department">
          {(c) => (
            <Select {...c} value={departmentId} placeholder="All departments" onChange={(e) => setUrl({ department: e.target.value })}>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          )}
        </FormField>
        <FormField label="Role">
          {(c) => (
            <Select {...c} value={role} placeholder="All roles" onChange={(e) => setUrl({ role: e.target.value })}>
              {ROLES.map((r) => (
                <option key={r} value={r}>
                  {ROLE_NAMES[r]}
                </option>
              ))}
            </Select>
          )}
        </FormField>
        <Checkbox label="Include inactive" className="h-10 items-center" checked={filters.includeInactive} onChange={(e) => setUrl({ inactive: e.target.checked ? 'true' : '' })} />
      </FilterBar>
      <DataTable
        caption="Users"
        columns={columns}
        rows={query.data?.items ?? []}
        getRowKey={(u) => u.id}
        isLoading={query.isPending}
        error={query.isError ? { message: toUserMessage(query.error), onRetry: () => void query.refetch() } : undefined}
        empty={empty}
      />
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
