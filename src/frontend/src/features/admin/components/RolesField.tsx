import type { UseFormRegisterReturn } from 'react-hook-form'
import { Checkbox } from '@/components/ui/Checkbox'
import type { Role } from '@/lib/api/types'
import { ROLES, ROLE_NAMES } from '@/lib/format/labels'

export interface RolesFieldProps {
  /** `register('roles')` from the form. */
  registration: UseFormRegisterReturn
  error?: string
  /**
   * Roles the person already holds when editing. Given, `Administrator` can be kept or removed but
   * not added: it is only given with a new account (ADR-0012, enforced by the server as 422
   * `user.admin_role_on_create_only`). Omit when creating.
   */
  held?: readonly Role[]
}

/** Checkbox group for a person's roles. Administrators have no request authority; the label says so. */
export function RolesField({ registration, error, held }: RolesFieldProps) {
  return (
    <fieldset className="m-0 min-w-0 border-0 p-0">
      <legend className="mb-2 text-sm font-medium text-ink">Roles</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {ROLES.map((role) => {
          const cannotAdd = role === 'Administrator' && held !== undefined && !held.includes(role)
          return (
            <Checkbox
              key={role}
              label={ROLE_NAMES[role]}
              hint={
                cannotAdd
                  ? 'Only given when an account is created.'
                  : role === 'Administrator'
                    ? 'Manages users and departments; cannot approve requests.'
                    : undefined
              }
              disabled={cannotAdd}
              value={role}
              {...registration}
            />
          )
        })}
      </div>
      {error && <p className="mt-1.5 text-sm text-danger">{error}</p>}
    </fieldset>
  )
}
