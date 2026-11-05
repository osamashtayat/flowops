import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Dialog } from '@/components/ui/Dialog'
import { FormField } from '@/components/ui/FormField'
import { Input } from '@/components/ui/Input'
import { useToast } from '@/components/ui/toastContext'

/** Review aid for FE-06: dialogs and toasts. Removed with FoundationPage. */
export function OverlaysDemo() {
  const toast = useToast()
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [running, setRunning] = useState(false)
  const [failure, setFailure] = useState<string>()

  function reject() {
    setRunning(true)
    window.setTimeout(() => {
      setRunning(false)
      setConfirmOpen(false)
      toast.success('Request FO-2026-000123 rejected. The requester has been notified.')
    }, 1000)
  }

  return (
    <Card title="Dialogs and toasts">
      <div className="flex flex-wrap gap-3">
        <Button variant="danger" onClick={() => setConfirmOpen(true)}>
          Reject with reason…
        </Button>
        <Button
          variant="secondary"
          onClick={() => {
            setFailure('Someone else already handled this task. The inbox has been refreshed.')
            setConfirmOpen(true)
          }}
        >
          Confirm with error…
        </Button>
        <Button variant="secondary" onClick={() => setFormOpen(true)}>
          Form dialog…
        </Button>
        <Button variant="ghost" onClick={() => toast.success('Request submitted. Your manager has been notified.')}>
          Success toast
        </Button>
        <Button variant="ghost" onClick={() => toast.info('Inbox updated.')}>
          Info toast
        </Button>
        <Button variant="ghost" onClick={() => toast.error('Could not save your changes. Try again.')}>
          Error toast
        </Button>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        title="Reject FO-2026-000123?"
        description="The requester is notified and the request is closed. This cannot be undone."
        confirmLabel="Reject request"
        tone="danger"
        reason={{ label: 'Reason', required: true, helperText: 'Visible to the requester.' }}
        loading={running}
        error={failure}
        onCancel={() => {
          setConfirmOpen(false)
          setFailure(undefined)
        }}
        onConfirm={reject}
      />

      <Dialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        title="Edit details"
        size="form"
        footer={
          <>
            <Button variant="secondary" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => setFormOpen(false)}>Save changes</Button>
          </>
        }
      >
        <FormField label="Display name">{(c) => <Input {...c} data-autofocus defaultValue="Dana Haddad" />}</FormField>
      </Dialog>
    </Card>
  )
}
