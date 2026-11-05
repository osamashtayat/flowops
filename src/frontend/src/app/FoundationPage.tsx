import { Controller } from 'react-hook-form'
import { z } from 'zod'
import { PageHeader } from '@/components/layout/PageHeader'
import { DataDisplayDemo } from '@/app/DataDisplayDemo'
import { OverlaysDemo } from '@/app/OverlaysDemo'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DateInput } from '@/components/ui/DateInput'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { MoneyInput } from '@/components/ui/MoneyInput'
import { Select } from '@/components/ui/Select'
import { StatusBadge } from '@/components/ui/StatusBadge'
import { Textarea } from '@/components/ui/Textarea'
import { REQUEST_LIMITS, upToCharacters } from '@/features/requests/limits'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { moneyAmountSchema } from '@/lib/forms/money'
import { requestStatusPresentation, type RequestStatus } from '@/lib/format/status'

const statuses: RequestStatus[] = ['InReview', 'Approved', 'Completed', 'Rejected']

/** Demo-only schema; real limits live in each feature's schemas.ts and mirror docs/API_CONTRACT.md. */
const demoSchema = z.object({
  itemName: z.string().trim().min(1, 'Enter the item name.').max(REQUEST_LIMITS.itemNameMax, `Use ${REQUEST_LIMITS.itemNameMax} characters or fewer.`),
  leaveType: z.enum(['Annual', 'Sick', 'Unpaid', 'Other'], { error: 'Choose a leave type.' }),
  startDate: z.string().min(1, 'Choose a start date.'),
  cost: moneyAmountSchema,
  justification: z.string().trim().min(1, 'Explain why this is needed.'),
})

/**
 * Temporary landing page showing the design foundation. Replaced by the dashboard in FE-12;
 * kept until then so reviewers can check tokens and primitives in a real browser.
 */
export function FoundationPage() {
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitSuccessful },
  } = useZodForm(demoSchema, {
    defaultValues: { itemName: '', leaveType: undefined, startDate: '', cost: '', justification: '' },
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title="FlowOps foundation"
        subtitle="Design tokens and shared components. Features arrive in FE-01 onwards."
        actions={<Button variant="secondary">Secondary action</Button>}
      />

      <Card title="Buttons">
        <div className="flex flex-wrap gap-3">
          <Button>Submit request</Button>
          <Button variant="secondary">Cancel</Button>
          <Button variant="ghost">View details</Button>
          <Button variant="danger">Reject request</Button>
          <Button loading>Saving</Button>
        </div>
      </Card>

      <Card title="Request statuses">
        <ul className="flex flex-wrap gap-3">
          {statuses.map((s) => (
            <li key={s}>
              <StatusBadge status={requestStatusPresentation(s)} />
            </li>
          ))}
        </ul>
      </Card>

      <Card title="Form: validated with zod, wired with react-hook-form">
        <form
          noValidate
          onSubmit={(event) => void handleSubmit(() => undefined)(event)}
          className="grid max-w-2xl gap-5"
        >
          <FormField label="Item name" required helperText={upToCharacters(REQUEST_LIMITS.itemNameMax)} error={fieldError(errors, 'itemName')}>
            {(c) => <Input {...c} {...register('itemName')} />}
          </FormField>

          <FormField label="Leave type" required error={fieldError(errors, 'leaveType')}>
            {(c) => (
              <Select {...c} placeholder="Select a type" {...register('leaveType')}>
                <option value="Annual">Annual</option>
                <option value="Sick">Sick</option>
                <option value="Unpaid">Unpaid</option>
                <option value="Other">Other</option>
              </Select>
            )}
          </FormField>

          <FormField label="Start date" required error={fieldError(errors, 'startDate')}>
            {(c) => <DateInput {...c} {...register('startDate')} />}
          </FormField>

          <FormField
            label="Estimated cost"
            required
            helperText="Up to 3 decimal places, in JOD."
            error={fieldError(errors, 'cost')}
          >
            {(c) => (
              <Controller
                control={control}
                name="cost"
                render={({ field }) => <MoneyInput {...c} {...field} />}
              />
            )}
          </FormField>

          <FormField
            label="Business justification"
            required
            helperText="Visible to your manager and Finance."
            error={fieldError(errors, 'justification')}
          >
            {(c) => <Textarea {...c} {...register('justification')} />}
          </FormField>

          <div className="flex items-center gap-3">
            <Button type="submit">Validate</Button>
            <Button type="button" variant="ghost" onClick={() => reset()}>
              Reset
            </Button>
            {isSubmitSuccessful && (
              <output className="text-sm text-success">All fields are valid.</output>
            )}
          </div>
        </form>
      </Card>

      <DataDisplayDemo />

      <OverlaysDemo />

      <Card title="Control states">
        <div className="grid max-w-2xl gap-5">
          <FormField label="Read-only" helperText="Shown on a muted surface.">
            {(c) => <Input {...c} readOnly value="Amman office" />}
          </FormField>
          <FormField label="Disabled">{(c) => <Input {...c} disabled value="Not editable" />}</FormField>
          <FormField label="With an error" error="This is what an error looks like.">
            {(c) => <Input {...c} defaultValue="Something wrong" />}
          </FormField>
        </div>
      </Card>
    </div>
  )
}
