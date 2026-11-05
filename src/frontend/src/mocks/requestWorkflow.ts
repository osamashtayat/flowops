import type { CurrentUserDto, RequestDetailsDto, RequestStage, Role, TaskAction, WorkTaskDto } from '@/lib/api/types'
import { findMockTask, mockEligibleActions, mockTask, mockTaskVersion, updateMockRequest } from './requestStore'

/**
 * A coarse imitation of the fixed processes (docs/WORKFLOWS.md) so decisions move a mock request
 * along. The real order, assignees and rules are the backend's (`FixedProcesses`).
 */
const NEXT: Record<'EquipmentPurchase' | 'Leave', Partial<Record<RequestStage, { stage: RequestStage; role: Role }>>> = {
  EquipmentPurchase: {
    ManagerApproval: { stage: 'FinanceApproval', role: 'FinanceOfficer' },
    FinanceApproval: { stage: 'ProcurementFulfillment', role: 'ProcurementOfficer' },
  },
  Leave: { ManagerApproval: { stage: 'HrConfirmation', role: 'HrOfficer' } },
}

export type DecisionFailure = { status: 400 | 403 | 404 | 409; code: string }

export function decideMockTask(
  user: CurrentUserDto,
  taskId: string,
  action: TaskAction,
  version: string,
  text: string | null,
): DecisionFailure | null {
  const found = findMockTask(taskId)
  if (!found) return { status: 404, code: 'not_found' }
  const { request, task } = found
  if (task.status === 'Done') return { status: 409, code: 'task.already_decided' }
  if (!mockEligibleActions(user, request, task).includes(action)) return { status: 403, code: 'task.not_eligible' }
  if (version !== mockTaskVersion()) return { status: 409, code: 'concurrency.conflict' }
  if (action === 'reject' && !text?.trim()) return { status: 400, code: 'validation' }

  const now = new Date().toISOString()
  const done: WorkTaskDto = {
    ...task,
    status: 'Done',
    outcome: action === 'reject' ? 'Rejected' : action === 'fulfill' ? 'Fulfilled' : 'Approved',
    decidedBy: { id: user.id, displayName: user.displayName },
    decidedAt: now,
    comment: text?.trim() || null,
  }
  const tasks = request.tasks.map((candidate) => (candidate.id === task.id ? done : candidate))
  updateMockRequest(advance(request, tasks, done, now))
  return null
}

function advance(request: RequestDetailsDto, tasks: WorkTaskDto[], done: WorkTaskDto, now: string): RequestDetailsDto {
  if (done.outcome === 'Rejected') return { ...request, tasks, status: 'Rejected', stage: 'Closed', closedAt: now, availableActions: null }
  const next = NEXT[request.type][done.stage]
  if (!next) return { ...request, tasks, status: 'Completed', stage: 'Closed', closedAt: now, availableActions: null }
  const nextTask = mockTask({ id: `${request.id}-t${tasks.length + 1}`, stage: next.stage, createdAt: now, assignee: { user: null, role: next.role } })
  return {
    ...request,
    tasks: [...tasks, nextTask],
    stage: next.stage,
    status: next.stage === 'ProcurementFulfillment' ? 'Approved' : 'InReview',
    availableActions: null,
  }
}
