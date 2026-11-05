import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { FormField } from './FormField'
import { MoneyInput } from './MoneyInput'

function Harness({ initial = '' }: { initial?: string }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <FormField label="Estimated cost" helperText="In JOD.">
        {(c) => <MoneyInput {...c} value={value} onChange={setValue} />}
      </FormField>
      <output data-testid="value">{value}</output>
    </>
  )
}

const field = () => screen.getByLabelText('Estimated cost')
const kept = () => screen.getByTestId('value').textContent

describe('MoneyInput', () => {
  it('accepts digits and up to three decimals, keeping the value as a string', async () => {
    render(<Harness />)
    await userEvent.type(field(), '1250.500')
    expect(field()).toHaveValue('1250.500')
    expect(kept()).toBe('1250.500')
  })

  it('ignores a fourth decimal digit', async () => {
    render(<Harness />)
    await userEvent.type(field(), '1.2345')
    expect(field()).toHaveValue('1.234')
  })

  it.each(['abc', '١٢٣', '-+'])('ignores invalid characters (%s)', async (text) => {
    render(<Harness />)
    await userEvent.type(field(), text)
    expect(field()).toHaveValue('')
    expect(kept()).toBe('')
  })

  it('drops a minus sign and exponent letters but keeps the digits around them', async () => {
    render(<Harness />)
    await userEvent.type(field(), '-1e3')
    expect(kept()).toBe('13')
  })

  it('allows only one decimal point', async () => {
    render(<Harness />)
    await userEvent.type(field(), '1.2.3')
    expect(field()).toHaveValue('1.23')
  })

  it('allows partial input while typing', async () => {
    render(<Harness />)
    await userEvent.type(field(), '12.')
    expect(kept()).toBe('12.')
  })

  it('pastes a valid value', async () => {
    render(<Harness />)
    await userEvent.click(field())
    await userEvent.paste('250.75')
    expect(kept()).toBe('250.75')
  })

  it('strips surrounding whitespace and thousands separators on paste', async () => {
    render(<Harness />)
    await userEvent.click(field())
    await userEvent.paste('  1,234.5 ')
    expect(kept()).toBe('1234.5')
  })

  it('rejects a paste with more than three decimals instead of rounding it', async () => {
    render(<Harness initial="7" />)
    await userEvent.click(field())
    await userEvent.paste('12.34567')
    expect(kept()).toBe('7')
  })

  it('rejects an ambiguous decimal comma instead of guessing', async () => {
    render(<Harness />)
    await userEvent.click(field())
    await userEvent.paste('1,5')
    expect(kept()).toBe('')
  })

  it('rejects pasted text that is not a number', async () => {
    render(<Harness />)
    await userEvent.click(field())
    await userEvent.paste('JOD 50')
    expect(kept()).toBe('')
  })

  it('shows a decimal keypad hint and the currency, and announces the currency', () => {
    render(<Harness />)
    expect(field()).toHaveAttribute('inputmode', 'decimal')
    expect(screen.getByText('JOD')).toBeVisible()
    expect(field()).toHaveAccessibleDescription('In JOD. JOD')
  })

  it('can be cleared with the keyboard', async () => {
    render(<Harness initial="12.5" />)
    await userEvent.click(field())
    await userEvent.keyboard('{Backspace>4/}')
    expect(kept()).toBe('')
  })
})
