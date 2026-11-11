import { useId, useState } from 'react'
import { Button } from '@/components/ui/Button'
import type { RequestType, TaskAction } from '@/lib/api/types'
import { decisionPresentation } from '../decisions'
import { DecisionDialog } from './DecisionDialog'

export interface DecisionActionsProps {
  taskId: string
  version: string
  requestType: RequestType
  requestNumber: string
  /** Exactly what the server put in `availableActions.actions`; the UI never adds to it. */
  actions: readonly TaskAction[]
}

/** The decision buttons for one task and the dialog they open. Used by the request page and the inbox. */
export function DecisionActions({ taskId, version, requestType, requestNumber, actions }: DecisionActionsProps) {
  const [chosen, setChosen] = useState<TaskAction | null>(null)
  // In the inbox every row has its own "Approve"; the request number tells them apart for screen readers.
  const describedBy = useId()
  return (
    <>
      <div className="flex flex-wrap gap-3">
        <span id={describedBy} className="sr-only">
          Request {requestNumber}
        </span>
        {actions.map((action) => (
          <Button key={action} variant={action === 'reject' ? 'secondary' : 'primary'} aria-describedby={describedBy} onClick={() => setChosen(action)}>
            {decisionPresentation(action, requestType).buttonLabel}
          </Button>
        ))}
      </div>
      <DecisionDialog
        action={chosen}
        taskId={taskId}
        version={version}
        requestType={requestType}
        requestNumber={requestNumber}
        onClose={() => setChosen(null)}
      />
    </>
  )
}
