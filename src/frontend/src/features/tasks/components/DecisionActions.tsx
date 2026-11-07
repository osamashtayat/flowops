import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import type { RequestStage, RequestType, TaskAction } from '@/lib/api/types'
import { decisionPresentation } from '../decisions'
import { DecisionDialog } from './DecisionDialog'

export interface DecisionActionsProps {
  taskId: string
  version: string
  stage: RequestStage
  requestType: RequestType
  requestNumber: string
  /** Exactly what the server put in `availableActions.actions`; the UI never adds to it. */
  actions: readonly TaskAction[]
}

/** The decision buttons for one task and the dialog they open. Used by the request page and the inbox. */
export function DecisionActions({ taskId, version, stage, requestType, requestNumber, actions }: DecisionActionsProps) {
  const [chosen, setChosen] = useState<TaskAction | null>(null)
  return (
    <>
      <div className="flex flex-wrap gap-3">
        {actions.map((action) => (
          <Button key={action} variant={action === 'reject' ? 'secondary' : 'primary'} onClick={() => setChosen(action)}>
            {decisionPresentation(action, stage, requestType).buttonLabel}
          </Button>
        ))}
      </div>
      <DecisionDialog
        action={chosen}
        taskId={taskId}
        version={version}
        stage={stage}
        requestType={requestType}
        requestNumber={requestNumber}
        onClose={() => setChosen(null)}
      />
    </>
  )
}
