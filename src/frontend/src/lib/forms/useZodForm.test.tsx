import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { z } from 'zod'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { fieldError, useZodForm } from './useZodForm'

const schema = z.object({
  first: z.string().min(1, 'Enter the first value.'),
  second: z.string().min(1, 'Enter the second value.'),
})

function Form({ onValid }: { onValid: (v: z.output<typeof schema>) => void }) {
  const { register, handleSubmit, formState } = useZodForm(schema, { defaultValues: { first: '', second: '' } })
  return (
    <form noValidate onSubmit={(e) => void handleSubmit(onValid)(e)}>
      <FormField label="First" required error={fieldError(formState.errors, 'first')}>
        {(c) => <Input {...c} {...register('first')} />}
      </FormField>
      <FormField label="Second" required error={fieldError(formState.errors, 'second')}>
        {(c) => <Input {...c} {...register('second')} />}
      </FormField>
      <button type="submit">Save</button>
    </form>
  )
}

describe('useZodForm + fieldError', () => {
  it('shows schema errors on the right fields, marks them invalid and focuses the first', async () => {
    const onValid = vi.fn<(values: z.output<typeof schema>) => void>()
    render(<Form onValid={onValid} />)
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Enter the first value.')).toBeInTheDocument()
    expect(screen.getByLabelText(/First/)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText(/Second/)).toHaveAccessibleDescription('Enter the second value.')
    expect(screen.getByLabelText(/First/)).toHaveFocus()
    expect(onValid).not.toHaveBeenCalled()
  })

  it('clears the error once the value is fixed and submits parsed values', async () => {
    const onValid = vi.fn<(values: z.output<typeof schema>) => void>()
    render(<Form onValid={onValid} />)
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await userEvent.type(screen.getByLabelText(/First/), 'a')
    await userEvent.type(screen.getByLabelText(/Second/), 'b')
    await waitFor(() => expect(screen.queryByText('Enter the first value.')).toBeNull())
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(onValid).toHaveBeenCalledOnce())
    expect(onValid.mock.calls[0]?.[0]).toEqual({ first: 'a', second: 'b' })
  })

  it('fieldError reads nested paths and ignores valid fields', () => {
    const errors = { estimatedCost: { amount: { type: 'custom', message: 'Too many decimals.' } } }
    expect(fieldError(errors, 'estimatedCost.amount')).toBe('Too many decimals.')
    expect(fieldError(errors, 'estimatedCost.currency')).toBeUndefined()
    expect(fieldError({}, 'anything')).toBeUndefined()
  })
})
