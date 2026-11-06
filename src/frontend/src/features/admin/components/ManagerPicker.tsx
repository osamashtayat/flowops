import { useEffect, useState } from 'react'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { toUserMessage } from '@/lib/api/problem'
import { useUsers } from '../api'

const SEARCH_DEBOUNCE_MS = 300
const RESULT_LIMIT = 20

export interface ManagerPickerProps {
  /** Selected manager id, "" for none. */
  value: string
  /** Name to show for `value` when it is not among the search results. */
  selectedName?: string
  /** Users who must not be offered: the person being edited and the signed-in administrator. */
  excludeIds: readonly string[]
  disabled?: boolean
  helperText?: string
  error?: string
  onChange: (id: string, displayName: string) => void
}

/** Searchable manager select: type to narrow the active users, then choose from the list. */
export function ManagerPicker({ value, selectedName, excludeIds, disabled, helperText, error, onChange }: ManagerPickerProps) {
  const [typed, setTyped] = useState('')
  const [search, setSearch] = useState('')
  useEffect(() => {
    const timer = setTimeout(() => setSearch(typed.trim()), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [typed])

  const users = useUsers({ search, departmentId: '', role: '', includeInactive: false, page: 1, pageSize: RESULT_LIMIT })
  const choices = (users.data?.items ?? []).filter((user) => !excludeIds.includes(user.id))
  const selectedIsListed = choices.some((user) => user.id === value)
  const failed = users.isError ? toUserMessage(users.error) : undefined

  return (
    <div className="flex flex-col gap-3">
      <FormField label="Find a manager" helperText="Type part of a name or email, then choose below.">
        {(c) => <Input {...c} type="search" value={typed} disabled={disabled} onChange={(e) => setTyped(e.target.value)} />}
      </FormField>
      <FormField label="Manager" helperText={helperText} error={error ?? failed}>
        {(c) => (
          <Select
            {...c}
            value={value}
            disabled={disabled}
            placeholder="No manager"
            onChange={(e) => {
              const option = e.target.selectedOptions[0]
              onChange(e.target.value, option?.textContent ?? '')
            }}
          >
            {value !== '' && !selectedIsListed && <option value={value}>{selectedName ?? 'Current manager'}</option>}
            {choices.map((user) => (
              <option key={user.id} value={user.id}>
                {user.displayName} ({user.email})
              </option>
            ))}
          </Select>
        )}
      </FormField>
    </div>
  )
}
