import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Checkbox } from '@/components/ui/Checkbox'
import { Dialog } from '@/components/ui/Dialog'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { useToast } from '@/components/ui/toastContext'
import { ApiError } from '@/lib/api/apiError'
import { ADMIN_LIMITS } from '@/lib/api/limits'
import { toUserMessage } from '@/lib/api/problem'
import type { DepartmentDto } from '@/lib/api/types'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useCreateDepartment, useUpdateDepartment } from '../api'
import { departmentSchema, type DepartmentValues } from '../schemas'

export interface DepartmentDialogProps {
  open: boolean
  /** The department being edited; omit to create. */
  department?: DepartmentDto
  onClose: () => void
}

export function DepartmentDialog({ open, department, onClose }: DepartmentDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} size="form" title={department ? `Edit ${department.name}` : 'New department'}>
      <DepartmentForm department={department} onClose={onClose} />
    </Dialog>
  )
}

function DepartmentForm({ department, onClose }: Omit<DepartmentDialogProps, 'open'>) {
  const create = useCreateDepartment()
  const update = useUpdateDepartment()
  const mutation = department ? update : create
  const toast = useToast()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useZodForm(departmentSchema, {
    defaultValues: { name: department?.name ?? '', code: department?.code ?? '', isActive: department?.isActive ?? true },
  })

  function submit(values: DepartmentValues) {
    const options = {
      onSuccess: (saved: DepartmentDto) => {
        toast.success(department ? `${saved.name} was updated.` : `${saved.name} was created.`)
        onClose()
      },
      onError: (error: unknown) => {
        if (!(error instanceof ApiError)) return
        for (const field of ['name', 'code'] as const) {
          const message = error.fieldErrors[field]?.[0]
          if (message) setError(field, { type: 'server', message })
        }
      },
    }
    if (department) update.mutate({ id: department.id, ...values }, options)
    else create.mutate({ name: values.name, code: values.code }, options)
  }

  const hasFieldErrors = mutation.error instanceof ApiError && Object.keys(mutation.error.fieldErrors).length > 0
  return (
    <form noValidate onSubmit={(event) => void handleSubmit(submit)(event)} className="flex flex-col gap-4">
      {mutation.error && !hasFieldErrors && <Alert tone="danger">{toUserMessage(mutation.error)}</Alert>}
      <FormField label="Name" required error={fieldError(errors, 'name')}>
        {(c) => <Input {...c} data-autofocus maxLength={ADMIN_LIMITS.departmentNameMax} {...register('name')} />}
      </FormField>
      <FormField label="Code" required helperText="Letters, digits and hyphens. Shown in capitals." error={fieldError(errors, 'code')}>
        {(c) => <Input {...c} maxLength={ADMIN_LIMITS.departmentCodeMax} {...register('code')} />}
      </FormField>
      {department && (
        <Checkbox label="Active" hint="Inactive departments keep their members but take no new ones." {...register('isActive')} />
      )}
      <div className="flex flex-wrap justify-end gap-3">
        <Button variant="secondary" onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button type="submit" loading={mutation.isPending}>
          {department ? 'Save changes' : 'Create department'}
        </Button>
      </div>
    </form>
  )
}
