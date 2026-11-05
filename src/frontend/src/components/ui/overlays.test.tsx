import { useState } from 'react'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Button } from './Button'
import { ConfirmDialog, type ConfirmDialogProps } from './ConfirmDialog'
import { Dialog } from './Dialog'
import { ToastProvider } from './Toast'
import { useToast } from './toastContext'

function DialogHarness({ dismissible = true }: { dismissible?: boolean }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button onClick={() => setOpen(true)}>Open dialog</button>
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title="Edit details"
        description="Change the details."
        dismissible={dismissible}
        footer={<Button onClick={() => setOpen(false)}>Done</Button>}
      >
        <input aria-label="Name" />
      </Dialog>
    </>
  )
}

describe('Dialog', () => {
  afterEach(() => {
    document.body.style.overflow = ''
  })

  it('is a modal dialog named by its title and described by its description', async () => {
    render(<DialogHarness />)
    expect(screen.queryByRole('dialog')).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit details' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAccessibleDescription('Change the details.')
  })

  it('moves focus into the dialog on open and back to the opener on close', async () => {
    render(<DialogHarness />)
    const opener = screen.getByRole('button', { name: 'Open dialog' })
    await userEvent.click(opener)
    expect(screen.getByRole('dialog')).toHaveFocus()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(opener).toHaveFocus()
  })

  it('closes with Esc, the Close button, and a backdrop click', async () => {
    render(<DialogHarness />)
    const open = () => userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))

    await open()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()

    await open()
    await userEvent.click(screen.getByRole('button', { name: 'Close dialog' }))
    expect(screen.queryByRole('dialog')).toBeNull()

    await open()
    const backdrop = screen.getByRole('dialog').parentElement as HTMLElement
    await userEvent.click(backdrop)
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('does not close when the click starts inside the panel', async () => {
    render(<DialogHarness />)
    await userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))
    await userEvent.click(screen.getByLabelText('Name'))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('traps Tab and Shift+Tab inside the dialog', async () => {
    render(<DialogHarness />)
    await userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))
    const close = screen.getByRole('button', { name: 'Close dialog' })
    const name = screen.getByLabelText('Name')
    const done = screen.getByRole('button', { name: 'Done' })

    await userEvent.tab()
    expect(close).toHaveFocus()
    await userEvent.tab()
    expect(name).toHaveFocus()
    await userEvent.tab()
    expect(done).toHaveFocus()
    await userEvent.tab() // wraps
    expect(close).toHaveFocus()
    await userEvent.tab({ shift: true }) // wraps backwards
    expect(done).toHaveFocus()
  })

  it('ignores Esc, backdrop and hides Close while not dismissible', async () => {
    render(<DialogHarness dismissible={false} />)
    await userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))
    await userEvent.keyboard('{Escape}')
    await userEvent.click(screen.getByRole('dialog').parentElement as HTMLElement)
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Close dialog' })).toBeNull()
  })

  it('locks page scroll while open and restores it', async () => {
    render(<DialogHarness />)
    await userEvent.click(screen.getByRole('button', { name: 'Open dialog' }))
    expect(document.body.style.overflow).toBe('hidden')
    await userEvent.keyboard('{Escape}')
    expect(document.body.style.overflow).toBe('')
  })
})

function renderConfirm(props: Partial<ConfirmDialogProps> = {}) {
  const onConfirm = vi.fn<(reason: string) => void>()
  const onCancel = vi.fn<() => void>()
  render(
    <ConfirmDialog
      open
      title="Reject FO-2026-000123?"
      description="The requester will be notified."
      confirmLabel="Reject request"
      tone="danger"
      onConfirm={onConfirm}
      onCancel={onCancel}
      {...props}
    />,
  )
  return { onConfirm, onCancel }
}

describe('ConfirmDialog', () => {
  it('names the action on the confirm button and focuses Cancel first (the safe choice)', () => {
    renderConfirm()
    const dialog = screen.getByRole('dialog', { name: 'Reject FO-2026-000123?' })
    expect(within(dialog).getByRole('button', { name: 'Reject request' })).toHaveClass('bg-danger')
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus()
    expect(screen.queryByRole('button', { name: 'OK' })).toBeNull()
  })

  it('confirms with an empty reason when there is no reason field', async () => {
    const { onConfirm } = renderConfirm()
    await userEvent.click(screen.getByRole('button', { name: 'Reject request' }))
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith('')
  })

  it('cancels from the Cancel button and from Esc', async () => {
    const { onCancel } = renderConfirm()
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    await userEvent.keyboard('{Escape}')
    expect(onCancel).toHaveBeenCalledTimes(2)
  })

  it('requires a reason: blocks confirm, shows a linked error, and keeps focus useful', async () => {
    const { onConfirm } = renderConfirm({ reason: { label: 'Reason', required: true, helperText: 'Shown to the requester.' } })
    const reason = screen.getByLabelText(/Reason/)
    expect(reason).toHaveFocus()
    expect(reason).toBeRequired()

    await userEvent.type(reason, '   ')
    await userEvent.click(screen.getByRole('button', { name: 'Reject request' }))
    expect(onConfirm).not.toHaveBeenCalled()
    expect(reason).toHaveAttribute('aria-invalid', 'true')
    expect(reason).toHaveAccessibleDescription('Shown to the requester. Enter a reason.')

    await userEvent.type(reason, ' Over budget ')
    await userEvent.click(screen.getByRole('button', { name: 'Reject request' }))
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith('Over budget')
  })

  it('is operable by keyboard: Tab to the confirm button and press Space', async () => {
    const { onConfirm } = renderConfirm()
    await userEvent.tab() // Cancel -> Reject request
    await userEvent.keyboard(' ')
    expect(onConfirm).toHaveBeenCalledOnce()
  })

  it('allows an optional reason to stay empty', async () => {
    const { onConfirm } = renderConfirm({ reason: { label: 'Comment' } })
    await userEvent.click(screen.getByRole('button', { name: 'Reject request' }))
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith('')
  })

  it('while loading: busy confirm button, disabled Cancel, cannot be dismissed or re-submitted', async () => {
    const { onConfirm, onCancel } = renderConfirm({ loading: true })
    expect(screen.getByRole('button', { name: 'Reject request' })).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled()
    await userEvent.keyboard('{Escape}')
    expect(onCancel).not.toHaveBeenCalled()
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('shows a failure message as an alert', () => {
    renderConfirm({ error: 'Someone else already handled this task.' })
    expect(screen.getByRole('alert')).toHaveTextContent('Someone else already handled this task.')
  })

  it('clears the reason each time it reopens', async () => {
    function Reopen() {
      const [open, setOpen] = useState(true)
      return (
        <>
          <button onClick={() => setOpen(true)}>again</button>
          <ConfirmDialog
            open={open}
            title="T"
            description="D"
            confirmLabel="Go"
            reason={{ label: 'Reason' }}
            onCancel={() => setOpen(false)}
            onConfirm={() => undefined}
          />
        </>
      )
    }
    render(<Reopen />)
    await userEvent.type(screen.getByLabelText('Reason'), 'draft')
    await userEvent.keyboard('{Escape}')
    await userEvent.click(screen.getByRole('button', { name: 'again' }))
    expect(screen.getByLabelText('Reason')).toHaveValue('')
  })
})

function ToastButtons() {
  const toast = useToast()
  return (
    <>
      <button onClick={() => toast.success('Request submitted.')}>success</button>
      <button onClick={() => toast.info('Heads up.')}>info</button>
      <button onClick={() => toast.error('Could not save.')}>error</button>
    </>
  )
}

const renderToasts = () =>
  render(
    <ToastProvider>
      <ToastButtons />
    </ToastProvider>,
  )

describe('Toast', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('announces success and info politely, errors assertively', async () => {
    renderToasts()
    await userEvent.click(screen.getByRole('button', { name: 'success' }))
    await userEvent.click(screen.getByRole('button', { name: 'info' }))
    await userEvent.click(screen.getByRole('button', { name: 'error' }))
    expect(screen.getByRole('status')).toHaveTextContent('Request submitted.')
    expect(screen.getByRole('status')).toHaveTextContent('Heads up.')
    expect(screen.getByRole('status')).not.toHaveTextContent('Could not save.')
    expect(screen.getByRole('alert')).toHaveTextContent('Could not save.')
  })

  it('has its live regions in the DOM before any toast appears', () => {
    renderToasts()
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
  })

  it('auto-dismisses success after 5 s but keeps errors', () => {
    vi.useFakeTimers()
    renderToasts()
    act(() => {
      screen.getByRole('button', { name: 'success' }).click()
      screen.getByRole('button', { name: 'error' }).click()
    })
    act(() => {
      vi.advanceTimersByTime(4900)
    })
    expect(screen.getByText('Request submitted.')).toBeInTheDocument()
    act(() => {
      vi.advanceTimersByTime(200)
    })
    expect(screen.queryByText('Request submitted.')).toBeNull()
    act(() => {
      vi.advanceTimersByTime(60_000)
    })
    expect(screen.getByText('Could not save.')).toBeInTheDocument()
  })

  it('pauses auto-dismiss while hovered', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderToasts()
    await userEvent.click(screen.getByRole('button', { name: 'success' }))
    await userEvent.hover(screen.getByText('Request submitted.'))
    act(() => {
      vi.advanceTimersByTime(20_000)
    })
    expect(screen.getByText('Request submitted.')).toBeInTheDocument()
    await userEvent.unhover(screen.getByText('Request submitted.'))
    act(() => {
      vi.advanceTimersByTime(5100)
    })
    expect(screen.queryByText('Request submitted.')).toBeNull()
  })

  it('dismisses a toast with its button, by keyboard', async () => {
    renderToasts()
    await userEvent.click(screen.getByRole('button', { name: 'error' }))
    const dismiss = screen.getByRole('button', { name: 'Dismiss notification' })
    dismiss.focus()
    await userEvent.keyboard('{Enter}')
    expect(screen.queryByText('Could not save.')).toBeNull()
  })

  it('useToast outside a provider fails loudly', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    expect(() => render(<ToastButtons />)).toThrow(/ToastProvider/)
    spy.mockRestore()
  })
})
