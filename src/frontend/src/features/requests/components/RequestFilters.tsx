import { FilterBar } from '@/components/ui/FilterBar'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { requestStatusPresentation } from '@/lib/format/status'
import type { RequestStatus, RequestType } from '@/lib/api/types'
import { REQUEST_STATUSES, REQUEST_TYPES, REQUEST_TYPE_LABELS } from '@/lib/format/labels'
import { REQUEST_LIMITS } from '@/lib/api/limits'
import { useDebouncedUrlSearch } from '@/lib/useDebouncedUrlSearch'

export interface RequestFilterValues {
  status: RequestStatus | ''
  type: RequestType | ''
  search: string
}

export interface RequestFiltersProps {
  values: RequestFilterValues
  onChange: (patch: Partial<RequestFilterValues>) => void
  onClear: () => void
}

/** Status, type and search. The URL is the source of truth; the search box keeps its own text only while the user is typing. */
export function RequestFilters({ values, onChange, onClear }: RequestFiltersProps) {
  const search = useDebouncedUrlSearch(values.search, (value) => onChange({ search: value }))

  return (
    <FilterBar
      label="Filter requests"
      hasActiveFilters={values.status !== '' || values.type !== '' || values.search !== ''}
      onClear={onClear}
    >
      <FormField label="Status">
        {(c) => (
          <Select {...c} value={values.status} placeholder="All statuses" onChange={(e) => onChange({ status: e.target.value as RequestStatus | '' })}>
            {REQUEST_STATUSES.map((status) => (
              <option key={status} value={status}>
                {requestStatusPresentation(status).label}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <FormField label="Type">
        {(c) => (
          <Select {...c} value={values.type} placeholder="All types" onChange={(e) => onChange({ type: e.target.value as RequestType | '' })}>
            {REQUEST_TYPES.map((type) => (
              <option key={type} value={type}>
                {REQUEST_TYPE_LABELS[type]}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <FormField label="Search" helperText="Request number or item name.">
        {(c) => (
          <Input {...c} type="search" value={search.value} maxLength={REQUEST_LIMITS.searchMax} onChange={(e) => search.onChange(e.target.value)} />
        )}
      </FormField>
    </FilterBar>
  )
}
