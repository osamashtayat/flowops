import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Card } from './Card'
import { DateInput } from './DateInput'
import { FormField } from './FormField'
import { Input } from './Input'
import { Select } from './Select'
import { Textarea } from './Textarea'

describe('Input / Textarea / Select / DateInput', () => {
  it('Tab moves through the controls in order and each shows the focus-visible outline class', async () => {
    render(
      <form>
        <FormField label="Name">{(c) => <Input {...c} />}</FormField>
        <FormField label="Notes">{(c) => <Textarea {...c} />}</FormField>
        <FormField label="Type">
          {(c) => (
            <Select {...c}>
              <option value="a">A</option>
            </Select>
          )}
        </FormField>
        <FormField label="Start">{(c) => <DateInput {...c} />}</FormField>
      </form>,
    )
    for (const name of ['Name', 'Notes', 'Type', 'Start']) {
      await userEvent.tab()
      const control = screen.getByLabelText(name)
      expect(control).toHaveFocus()
      expect(control).toHaveClass('focus-visible:outline-2', 'focus-visible:outline-primary')
    }
  })

  it('uses the strong border for outlines and flips to danger when invalid', () => {
    render(
      <FormField label="Name" error="Required.">
        {(c) => <Input {...c} />}
      </FormField>,
    )
    const input = screen.getByLabelText('Name')
    expect(input).toHaveClass('border-border-strong', 'aria-[invalid=true]:border-danger')
    expect(input).not.toHaveClass('border-border')
  })

  it('Textarea accepts multi-line typing', async () => {
    render(<FormField label="Notes">{(c) => <Textarea {...c} />}</FormField>)
    await userEvent.type(screen.getByLabelText('Notes'), 'a{Enter}b')
    expect(screen.getByLabelText('Notes')).toHaveValue('a\nb')
  })

  it('Select is focusable, shows its placeholder option, and changes value', async () => {
    render(
      <FormField label="Leave type">
        {(c) => (
          <Select {...c} placeholder="Select a type">
            <option value="Annual">Annual</option>
            <option value="Sick">Sick</option>
          </Select>
        )}
      </FormField>,
    )
    const select = screen.getByLabelText('Leave type')
    expect(select).toHaveValue('')
    await userEvent.tab()
    expect(select).toHaveFocus()
    // Opening and arrowing through a native <select> is browser behaviour; user-event can only select.
    await userEvent.selectOptions(select, 'Sick')
    expect(select).toHaveValue('Sick')
  })

  it('DateInput holds yyyy-MM-dd and respects min', async () => {
    render(<FormField label="End date">{(c) => <DateInput {...c} min="2026-11-05" />}</FormField>)
    const date = screen.getByLabelText('End date')
    expect(date).toHaveAttribute('type', 'date')
    expect(date).toHaveAttribute('min', '2026-11-05')
    await userEvent.type(date, '2026-11-10')
    expect(date).toHaveValue('2026-11-10')
  })

  it('read-only inputs are focusable but not editable', async () => {
    render(<FormField label="Department">{(c) => <Input {...c} readOnly defaultValue="Finance" />}</FormField>)
    await userEvent.tab()
    expect(screen.getByLabelText('Department')).toHaveFocus()
    await userEvent.keyboard('x')
    expect(screen.getByLabelText('Department')).toHaveValue('Finance')
  })
})

describe('Card', () => {
  it('renders its title as a heading, at the requested level', () => {
    render(
      <>
        <Card title="Details">body</Card>
        <Card title="Sub" headingLevel={3}>
          body
        </Card>
      </>,
    )
    expect(screen.getByRole('heading', { level: 3, name: 'Sub' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Details' })).toBeInTheDocument()
  })

  it('renders without a header when untitled', () => {
    render(<Card>body</Card>)
    expect(screen.queryByRole('heading')).toBeNull()
  })
})
