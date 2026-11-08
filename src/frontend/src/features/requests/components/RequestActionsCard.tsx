import { Card } from '@/components/ui/Card'
import { DecisionActions } from '@/features/tasks/components/DecisionActions'
import type { RequestDetailsDto } from '@/lib/api/types'

/**
 * The decision buttons, exactly as `availableActions` lists them. The stage comes from the task the
 * server named; nothing here decides whether the viewer may act.
 */
export function RequestActionsCard({ request }: { request: RequestDetailsDto }) {
  const available = request.availableActions
  const task = request.tasks.find((candidate) => candidate.id === available?.taskId)
  if (!available || !task || available.actions.length === 0) return null
  return (
    <Card title="Your action">
      <p className="mb-4 text-sm text-ink-muted">This request is waiting for your decision.</p>
      {/* Keyed by task: when a refresh hands this page a different task, an open dialog must not carry over to it. */}
      <DecisionActions
        key={available.taskId}
        taskId={available.taskId}
        version={available.version}
        stage={task.stage}
        requestType={request.type}
        requestNumber={request.number}
        actions={available.actions}
      />
    </Card>
  )
}
