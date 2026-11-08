import { useNavigate } from 'react-router'
import { useToast } from '@/components/ui/toastContext'
import type { Role, UserDto } from '@/lib/api/types'
import { useCreateUser, useSaveUser } from './api'
import type { UserFormValues } from './schemas'

function sameRoles(a: readonly Role[], b: readonly Role[]): boolean {
  return [...a].sort().join() === [...b].sort().join()
}

/**
 * What the user form sends and what happens after: create, or save the profile and (when changed)
 * the roles. On the administrator's own record the locked fields go back exactly as the server has
 * them, never as the read-only controls hold them; own roles answer 202 and wait for another
 * administrator (ADR-0012). The server stays the authority for every rule.
 */
export function useUserFormSubmit(user: UserDto | undefined, isSelf: boolean) {
  const navigate = useNavigate()
  const toast = useToast()
  const create = useCreateUser()
  const save = useSaveUser()

  function submit(values: UserFormValues) {
    const links = { departmentId: values.departmentId || null, managerId: values.managerId || null }
    if (!user) {
      const body = { email: values.email, displayName: values.displayName, initialPassword: values.initialPassword, roles: values.roles, ...links }
      create.mutate(body, {
        onSuccess: (created) => {
          toast.success(`${created.displayName} was created.`)
          void navigate('/admin/users')
        },
      })
      return
    }
    const profile = isSelf
      ? { displayName: values.displayName, departmentId: links.departmentId, managerId: user.manager?.id ?? null, isActive: user.isActive }
      : { displayName: values.displayName, isActive: values.isActive, ...links }
    const roles = sameRoles(values.roles, user.roles) ? undefined : values.roles
    save.mutate(
      { id: user.id, profile, roles },
      {
        onSuccess: ({ user: saved, roleChange }) => {
          // The server did not apply our own roles; they wait for another administrator.
          toast.success(roleChange ? 'Role change sent. Waiting for another administrator.' : `${saved.displayName} was updated.`)
          void navigate(roleChange ? '/admin/role-changes' : '/admin/users')
        },
      },
    )
  }

  return { submit, mutation: user ? save : create }
}
