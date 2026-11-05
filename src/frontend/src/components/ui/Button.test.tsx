import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Button } from './Button'

describe('Button', () => {
  it('defaults to type="button" so it never submits forms by accident', () => {
    render(<Button>Save</Button>)
    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('type', 'button')
  })

  it('calls onClick when pressed', async () => {
    const onClick = vi.fn<() => void>()
    render(<Button onClick={onClick}>Approve</Button>)
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    expect(onClick).toHaveBeenCalledOnce()
  })

  it('is disabled and busy while loading, and ignores clicks', async () => {
    const onClick = vi.fn<() => void>()
    render(
      <Button loading onClick={onClick}>
        Saving
      </Button>,
    )
    const button = screen.getByRole('button', { name: 'Saving' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    await userEvent.click(button)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('applies design-token classes for the danger variant', () => {
    render(<Button variant="danger">Reject</Button>)
    expect(screen.getByRole('button', { name: 'Reject' })).toHaveClass('bg-danger', 'text-on-primary')
  })
})
