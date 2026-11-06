import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Checkbox } from '@/components/ui/Checkbox'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { useToast } from '@/components/ui/toastContext'
import { useSession } from '@/features/session/api'
import { ApiError } from '@/lib/api/apiError'
import { ADMIN_LIMITS } from '@/lib/api/limits'
import { toUserMessage } from '@/lib/api/problem'
import type { Role, UserDto } from '@/lib/api/types'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useCreateUser, useDepartments, useSaveUser } from '../api'
import { userFormSchema, type UserFormValues } from '../schemas'
import { ManagerPicker } from './ManagerPicker'
import { RolesField } from './RolesField'

const FIELDS = ['displayName', 'email', 'initialPassword', 'departmentId', 'managerId', 'roles'] as const

/** The first server 400 message for a form field. */
function serverMessage(error: unknown, field: string): string | undefined {
  return error instanceof ApiError ? error.fieldErrors[field]?.[0] : undefined
}

function sameRoles(a: readonly Role[], b: readonly Role[]): boolean {
  return [...a].sort().join() === [...b].sort().join()
}

export interface UserFormProps {
  /** The user being edited; omit to create. */
  user?: UserDto
}

/**
 * Create / edit a user. On the signed-in administrator's own record the roles, manager and active
 * switch are read-only and an explanation is shown: the server refuses those changes (422
 * `user.self_administration`, docs/AUTHORIZATION.md §2). The server stays the authority; if it
 * answers 422 anyway, its mapped message appears in the banner.
 */
export function UserForm({ user }: UserFormProps) {
  const { user: me } = useSession()
  const isSelf = user !== undefined && user.id === me?.id
  const navigate = useNavigate()
  const toast = useToast()
  const create = useCreateUser()
  const save = useSaveUser()
  const mutation = user ? save : create
  const departments = useDepartments(true)
  const [managerName, setManagerName] = useState(user?.manager?.displayName)

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useZodForm(userFormSchema(user ? 'edit' : 'create'), {
    defaultValues: {
      displayName: user?.displayName ?? '',
      email: user?.email ?? '',
      initialPassword: '',
      departmentId: user?.department?.id ?? '',
      managerId: user?.manager?.id ?? '',
      roles: user?.roles ?? ['Employee'],
      isActive: user?.isActive ?? true,
    },
  })

  function submit(values: UserFormValues) {
    const links = { departmentId: values.departmentId || null, managerId: values.managerId || null }
    const done = (saved: UserDto) => {
      toast.success(`${saved.displayName} was ${user ? 'updated' : 'created'}.`)
      void navigate('/admin/users')
    }
    if (!user) {
      create.mutate({ email: values.email, displayName: values.displayName, initialPassword: values.initialPassword, roles: values.roles, ...links }, { onSuccess: done })
      return
    }
    // Own record: send what the server already has for the locked fields, never what the read-only controls hold.
    const profile = isSelf
      ? { displayName: values.displayName, departmentId: links.departmentId, managerId: user.manager?.id ?? null, isActive: user.isActive }
      : { displayName: values.displayName, isActive: values.isActive, ...links }
    const rolesChanged = !isSelf && !sameRoles(values.roles, user.roles)
    save.mutate({ id: user.id, profile, roles: rolesChanged ? values.roles : undefined }, { onSuccess: done })
  }

  const failure = (field: string) => serverMessage(mutation.error, field)
  const banner = mutation.error && !FIELDS.some(failure) ? toUserMessage(mutation.error) : null
  const departmentChoices = (departments.data ?? []).filter((d) => d.isActive || d.id === user?.department?.id)

  return (
    <form noValidate onSubmit={(event) => void handleSubmit(submit)(event)} className="grid max-w-160 gap-5">
      {isSelf && (
        <Alert tone="info">This is your own account. Another administrator has to change your roles, your manager or your active status.</Alert>
      )}
      {banner && <Alert tone="danger">{banner}</Alert>}
      <FormField label="Display name" required error={fieldError(errors, 'displayName') ?? failure('displayName')}>
        {(c) => <Input {...c} maxLength={ADMIN_LIMITS.displayNameMax} {...register('displayName')} />}
      </FormField>
      <FormField
        label="Email"
        required={!user}
        helperText={user ? 'The email cannot be changed.' : undefined}
        error={fieldError(errors, 'email') ?? failure('email')}
      >
        {(c) => <Input {...c} type="email" autoComplete="off" maxLength={ADMIN_LIMITS.emailMax} readOnly={Boolean(user)} {...register('email')} />}
      </FormField>
      {!user && (
        <FormField
          label="Initial password"
          required
          helperText={`At least ${ADMIN_LIMITS.passwordMin} characters. The person must change it at first sign-in.`}
          error={fieldError(errors, 'initialPassword') ?? failure('initialPassword')}
        >
          {(c) => <Input {...c} type="password" autoComplete="new-password" {...register('initialPassword')} />}
        </FormField>
      )}
      <FormField label="Department" error={failure('departmentId')}>
        {(c) => (
          <Select {...c} placeholder="No department" {...register('departmentId')}>
            {departmentChoices.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
                {d.isActive ? '' : ' (inactive)'}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <ManagerPicker
        value={isSelf ? (user.manager?.id ?? '') : watch('managerId')}
        selectedName={managerName}
        excludeIds={[user?.id, me?.id].filter((id): id is string => id !== undefined)}
        disabled={isSelf}
        error={failure('managerId')}
        onChange={(id, name) => {
          setValue('managerId', id, { shouldDirty: true })
          setManagerName(name)
        }}
      />
      <RolesField registration={register('roles')} locked={isSelf ? user.roles : undefined} error={failure('roles')} />
      {user &&
        (isSelf ? (
          <Checkbox label="Active" checked={user.isActive} disabled readOnly />
        ) : (
          <Checkbox label="Active" hint="Inactive people cannot sign in." {...register('isActive')} />
        ))}
      <div className="flex flex-wrap justify-end gap-3">
        <Button variant="ghost" onClick={() => void navigate('/admin/users')}>
          Cancel
        </Button>
        <Button type="submit" loading={mutation.isPending}>
          {user ? 'Save changes' : 'Create user'}
        </Button>
      </div>
    </form>
  )
}
