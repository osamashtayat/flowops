import type { UseFormRegisterReturn } from 'react-hook-form'
import { Checkbox } from '@/components/ui/Checkbox'
import type { Role } from '@/lib/api/types'
import { ROLES, ROLE_NAMES } from '../labels'

export interface RolesFieldProps {
  /** `register('roles')` from the form; ignored when `locked`. */
  registration: UseFormRegisterReturn
  /** Shows these roles read-only instead (the administrator's own record). */
  locked?: readonly Role[]
  error?: string
}

/** Checkbox group for a person's roles. Administrators have no request authority; the label says so. */
export function RolesField({ registration, locked, error }: RolesFieldProps) {
  return (
    <fieldset className="m-0 min-w-0 border-0 p-0">
      <legend className="mb-2 text-sm font-medium text-ink">Roles</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {ROLES.map((role) =>
          locked ? (
            <Checkbox key={role} label={ROLE_NAMES[role]} checked={locked.includes(role)} disabled readOnly />
          ) : (
            <Checkbox
              key={role}
              label={ROLE_NAMES[role]}
              hint={role === 'Administrator' ? 'Manages users and departments; cannot approve requests.' : undefined}
              value={role}
              {...registration}
            />
          ),
        )}
      </div>
      {error && <p className="mt-1.5 text-sm text-danger">{error}</p>}
    </fieldset>
  )
}
