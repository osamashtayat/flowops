import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FormField } from './FormField'
import { Input } from './Input'

function renderField(props: Partial<Parameters<typeof FormField>[0]> = {}) {
  return render(
    <FormField label="Item name" {...props}>
      {(c) => <Input {...c} />}
    </FormField>,
  )
}

describe('FormField', () => {
  it('associates the label with the control', () => {
    renderField()
    expect(screen.getByLabelText('Item name')).toBeInstanceOf(HTMLInputElement)
  })

  it('marks required visibly (hidden from AT) and natively on the control', () => {
    renderField({ required: true })
    const input = screen.getByRole('textbox', { name: 'Item name' })
    expect(input).toBeRequired()
    const marker = screen.getByText('*')
    expect(marker).toHaveAttribute('aria-hidden', 'true')
  })

  it('has no required marker or attribute when optional', () => {
    renderField()
    expect(screen.queryByText('*')).toBeNull()
    expect(screen.getByLabelText('Item name')).not.toBeRequired()
  })

  it('links helper text through aria-describedby', () => {
    renderField({ helperText: 'Shown under the field.' })
    expect(screen.getByLabelText('Item name')).toHaveAccessibleDescription('Shown under the field.')
  })

  it('links the error, sets aria-invalid, and keeps the helper in the description', () => {
    renderField({ helperText: 'Shown under the field.', error: 'Enter the item name.' })
    const input = screen.getByLabelText('Item name')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAccessibleDescription('Shown under the field. Enter the item name.')
    expect(screen.getByText('Enter the item name.')).toHaveClass('text-danger')
  })

  it('is valid and undescribed without helper or error', () => {
    renderField()
    const input = screen.getByLabelText('Item name')
    expect(input).not.toHaveAttribute('aria-invalid')
    expect(input).not.toHaveAttribute('aria-describedby')
  })

  it('gives each field its own ids', () => {
    render(
      <>
        <FormField label="First">{(c) => <Input {...c} />}</FormField>
        <FormField label="Second">{(c) => <Input {...c} />}</FormField>
      </>,
    )
    expect(screen.getByLabelText('First').id).not.toBe(screen.getByLabelText('Second').id)
  })
})
