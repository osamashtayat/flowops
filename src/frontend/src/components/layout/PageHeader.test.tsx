import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PageHeader } from './PageHeader'

describe('PageHeader', () => {
  it('renders the title as the single h1 with subtitle and actions', () => {
    render(<PageHeader title="New request" subtitle="Choose a type." actions={<button type="button">Cancel</button>} />)
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.getByRole('heading', { level: 1, name: 'New request' })).toBeInTheDocument()
    expect(screen.getByText('Choose a type.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
  })
})
