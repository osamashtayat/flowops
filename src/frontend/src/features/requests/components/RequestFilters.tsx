import { useEffect, useState } from 'react'
import { FilterBar } from '@/components/ui/FilterBar'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { requestStatusPresentation } from '@/lib/format/status'
import type { RequestStatus, RequestType } from '@/lib/api/types'
import { REQUEST_STATUSES, REQUEST_TYPES, REQUEST_TYPE_LABELS } from '../labels'
import { REQUEST_LIMITS } from '../limits'

/** How long typing must pause before the search reaches the URL (docs/UI_SPEC.md §4.3). */
const SEARCH_DEBOUNCE_MS = 300

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
  // `typed` is what the user has typed since the URL last changed; null means "show the URL's value".
  const [typed, setTyped] = useState<string | null>(null)
  const [seenUrlSearch, setSeenUrlSearch] = useState(values.search)
  if (seenUrlSearch !== values.search) {
    // The URL changed (our own debounced push, Clear filters, back button): follow it.
    setSeenUrlSearch(values.search)
    setTyped(null)
  }
  const shown = typed ?? values.search

  useEffect(() => {
    if (typed === null || typed.trim() === values.search) return
    const timer = setTimeout(() => onChange({ search: typed.trim() }), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [typed, values.search, onChange])

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
          <Input {...c} type="search" value={shown} maxLength={REQUEST_LIMITS.searchMax} onChange={(e) => setTyped(e.target.value)} />
        )}
      </FormField>
    </FilterBar>
  )
}
