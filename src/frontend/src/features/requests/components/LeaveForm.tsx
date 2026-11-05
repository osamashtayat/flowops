import { useWatch } from 'react-hook-form'
import { DateInput } from '@/components/ui/DateInput'
import { FormField } from '@/components/ui/FormField'
import { Select } from '@/components/ui/Select'
import { Textarea } from '@/components/ui/Textarea'
import { calendarDaysInclusive, todayInCompanyZone } from '@/lib/format/date'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useSubmitLeave } from '../api'
import { LEAVE_TYPES, LEAVE_TYPE_LABELS } from '../labels'
import { REQUEST_LIMITS, upToCharacters } from '../limits'
import { leaveSchema, type LeaveValues } from '../schemas'
import { useSubmitOutcome } from '../useSubmitOutcome'
import { RequestFormFrame } from './RequestFormFrame'

const FIELDS = ['startDate', 'endDate', 'leaveType', 'reason'] as const

function daysHint(startDate: string, endDate: string): string | undefined {
  const days = calendarDaysInclusive(startDate, endDate)
  return days === null ? undefined : `${days} calendar ${days === 1 ? 'day' : 'days'}`
}

export function LeaveForm() {
  const submit = useSubmitLeave()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useZodForm(leaveSchema, {
    defaultValues: { startDate: '', endDate: '', leaveType: 'Annual', reason: '' },
  })
  const outcome = useSubmitOutcome(setError, FIELDS)
  const [startDate, endDate] = useWatch({ control, name: ['startDate', 'endDate'] })

  return (
    <RequestFormFrame
      onSubmit={(event) => void handleSubmit((values: LeaveValues) => submit.mutate(values, outcome))(event)}
      isSubmitting={submit.isPending}
      submitError={submit.error}
    >
      <div className="grid gap-5 sm:grid-cols-2">
        <FormField label="Start date" required error={fieldError(errors, 'startDate')}>
          {(c) => <DateInput {...c} min={todayInCompanyZone()} {...register('startDate')} />}
        </FormField>
        <FormField label="End date" required helperText={daysHint(startDate, endDate)} error={fieldError(errors, 'endDate')}>
          {(c) => <DateInput {...c} min={startDate || todayInCompanyZone()} {...register('endDate')} />}
        </FormField>
      </div>
      <FormField label="Leave type" required error={fieldError(errors, 'leaveType')}>
        {(c) => (
          <Select {...c} {...register('leaveType')}>
            {LEAVE_TYPES.map((type) => (
              <option key={type} value={type}>
                {LEAVE_TYPE_LABELS[type]}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <FormField
        label="Reason"
        helperText={`Visible only to your manager and HR. ${upToCharacters(REQUEST_LIMITS.leaveReasonMax)}`}
        error={fieldError(errors, 'reason')}
      >
        {(c) => <Textarea {...c} {...register('reason')} />}
      </FormField>
    </RequestFormFrame>
  )
}
