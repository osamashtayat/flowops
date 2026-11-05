import { Controller } from 'react-hook-form'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { MoneyInput } from '@/components/ui/MoneyInput'
import { Textarea } from '@/components/ui/Textarea'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { useSubmitPurchase } from '../api'
import { REQUEST_LIMITS, upToCharacters } from '../limits'
import { purchaseSchema, type PurchaseValues } from '../schemas'
import { useSubmitOutcome } from '../useSubmitOutcome'
import { RequestFormFrame } from './RequestFormFrame'

const FIELDS = ['itemName', 'description', 'estimatedCost', 'justification'] as const

export function PurchaseForm() {
  const submit = useSubmitPurchase()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useZodForm(purchaseSchema, {
    defaultValues: { itemName: '', description: '', estimatedCost: '', justification: '' },
  })
  const outcome = useSubmitOutcome(setError, FIELDS)

  return (
    <RequestFormFrame
      onSubmit={(event) => void handleSubmit((values: PurchaseValues) => submit.mutate(values, outcome))(event)}
      isSubmitting={submit.isPending}
      submitError={submit.error}
    >
      <FormField label="Item name" required helperText={upToCharacters(REQUEST_LIMITS.itemNameMax)} error={fieldError(errors, 'itemName')}>
        {(c) => <Input {...c} {...register('itemName')} />}
      </FormField>
      <FormField label="Description" required helperText={upToCharacters(REQUEST_LIMITS.descriptionMax)} error={fieldError(errors, 'description')}>
        {(c) => <Textarea {...c} {...register('description')} />}
      </FormField>
      <FormField label="Estimated cost" required helperText="In Jordanian dinars, up to 3 decimals." error={fieldError(errors, 'estimatedCost')}>
        {(c) => (
          <Controller
            control={control}
            name="estimatedCost"
            render={({ field }) => <MoneyInput {...c} {...field} />}
          />
        )}
      </FormField>
      <FormField label="Business justification" required helperText={upToCharacters(REQUEST_LIMITS.justificationMax)} error={fieldError(errors, 'justification')}>
        {(c) => <Textarea {...c} {...register('justification')} />}
      </FormField>
    </RequestFormFrame>
  )
}
