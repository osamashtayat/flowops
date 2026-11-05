import { zodResolver } from '@hookform/resolvers/zod'
import { get, useForm, type FieldErrors, type FieldValues, type UseFormProps, type UseFormReturn } from 'react-hook-form'
import type { z } from 'zod'

/**
 * react-hook-form wired to a zod schema. Validates on blur first, then live (`onTouched`), and
 * relies on RHF's default of focusing the first invalid field on submit — which is why every
 * control forwards its ref.
 */
export function useZodForm<TInput extends FieldValues, TOutput extends FieldValues>(
  schema: z.ZodType<TOutput, TInput>,
  options?: Omit<UseFormProps<TInput, unknown, TOutput>, 'resolver'>,
): UseFormReturn<TInput, unknown, TOutput> {
  return useForm<TInput, unknown, TOutput>({
    mode: 'onTouched',
    ...options,
    resolver: zodResolver(schema),
  })
}

/**
 * The message to show in `<FormField error={…}>` for a (possibly nested) field path such as
 * `estimatedCost.amount`. Returns `undefined` when the field is valid.
 */
export function fieldError(errors: FieldErrors, name: string): string | undefined {
  const message: unknown = get(errors, `${name}.message`)
  return typeof message === 'string' && message.length > 0 ? message : undefined
}
