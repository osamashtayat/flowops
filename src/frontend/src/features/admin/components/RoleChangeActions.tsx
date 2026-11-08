import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toastContext'
import type { RoleChangeAction, RoleChangeDto } from '@/lib/api/types'
import { ADMIN_LIMITS } from '@/lib/api/limits'
import { toUserMessage } from '@/lib/api/problem'
import { useDecideRoleChange } from '../api'

/** Words for each action. The server decides which are available (`availableActions`); this only names them. */
const PRESENTATION: Record<RoleChangeAction, { button: string; variant: 'primary' | 'secondary'; tone: 'primary' | 'danger'; done: string; describe: (name: string) => { title: string; description: string } }> = {
  approve: {
    button: 'Approve',
    variant: 'primary',
    tone: 'primary',
    done: 'Role change approved.',
    describe: (name) => ({ title: `Approve the role change for ${name}?`, description: 'Their roles change immediately and they are told.' }),
  },
  reject: {
    button: 'Reject',
    variant: 'secondary',
    tone: 'danger',
    done: 'Role change rejected.',
    describe: (name) => ({ title: `Reject the role change for ${name}?`, description: 'Their roles stay as they are and they are told.' }),
  },
  cancel: {
    button: 'Cancel change',
    variant: 'secondary',
    tone: 'danger',
    done: 'Role change cancelled.',
    describe: () => ({ title: 'Cancel your role change?', description: 'Nothing changes. You can propose a new one afterwards.' }),
  },
}

/** Approve / reject / cancel buttons for one role change, each behind a confirmation dialog. */
export function RoleChangeActions({ change }: { change: RoleChangeDto }) {
  const [open, setOpen] = useState<RoleChangeAction | null>(null)
  const toast = useToast()
  const decide = useDecideRoleChange({
    onSuccess: (input) => {
      toast.success(PRESENTATION[input.action].done)
      close()
    },
    // Stale and already-decided answers refetch the list. The dialog shows why, but when the refetch drops the row
    // (it is no longer Pending) the dialog goes with it, so the same message also goes to a toast the user can't miss.
    onError: (error) => toast.error(toUserMessage(error)),
  })

  function close() {
    setOpen(null)
    decide.reset()
  }

  function confirm(action: RoleChangeAction, reason: string) {
    decide.mutate({ id: change.id, action, version: change.version, reason })
  }

  return (
    <div className="flex flex-wrap gap-2">
      {change.availableActions.map((action) => (
        <Button key={action} size="sm" variant={PRESENTATION[action].variant} onClick={() => setOpen(action)}>
          {PRESENTATION[action].button}
        </Button>
      ))}
      {open && (
        <ConfirmDialog
          open
          {...PRESENTATION[open].describe(change.user.displayName)}
          tone={PRESENTATION[open].tone}
          confirmLabel={open === 'cancel' ? 'Cancel change' : `${PRESENTATION[open].button} change`}
          cancelLabel={open === 'cancel' ? 'Keep it' : 'Back'}
          reason={open === 'reject' ? { label: 'Reason', required: true, helperText: 'Shown to the administrator who proposed the change.', maxLength: ADMIN_LIMITS.roleChangeReasonMax } : undefined}
          loading={decide.isPending}
          error={decide.error ? toUserMessage(decide.error) : undefined}
          onCancel={close}
          onConfirm={(reason) => confirm(open, reason)}
        />
      )}
    </div>
  )
}
