import type { RequestDetailsDto, RequestStage, RequestType, StepRef } from '@/lib/api/types'

/**
 * The mock's stand-in for the server's step keys, names and route (docs/API_CONTRACT.md §4, ADR-0014 Q2/Q5).
 * Imitated coarsely so screens can be exercised; the real mapping lives in the backend (`StepRef`, `RequestRoute`).
 */
const STEPS: Record<Exclude<RequestStage, 'Closed'>, StepRef> = {
  ManagerApproval: { key: 'manager-approval', name: 'Manager approval' },
  FinanceApproval: { key: 'finance-approval', name: 'Finance approval' },
  HrConfirmation: { key: 'hr-confirmation', name: 'HR confirmation' },
  ProcurementFulfillment: { key: 'procurement-fulfillment', name: 'Procurement fulfilment' },
}

const ORDER: Record<RequestType, Exclude<RequestStage, 'Closed'>[]> = {
  EquipmentPurchase: ['ManagerApproval', 'FinanceApproval', 'ProcurementFulfillment'],
  Leave: ['ManagerApproval', 'HrConfirmation'],
}

export const mockTaskStep = (stage: RequestStage): StepRef => (stage === 'Closed' ? STEPS.ManagerApproval : STEPS[stage])

export const mockStep = (stage: RequestStage | null): StepRef | null => (stage === null || stage === 'Closed' ? null : STEPS[stage])

/** Recomputes the derived `step` and `route` from the stored stage and tasks, as the server would on every read. */
export function withMockRoute(request: RequestDetailsDto): RequestDetailsDto {
  const route: RequestDetailsDto['route'] = request.tasks.map((task) => ({
    step: mockTaskStep(task.stage),
    kind: task.kind,
    state: task.status === 'Open' ? 'Current' : 'Done',
  }))
  const open = request.tasks.find((task) => task.status === 'Open')
  if (open && open.stage !== 'Closed') {
    const order = ORDER[request.type]
    for (const stage of order.slice(order.indexOf(open.stage) + 1)) {
      route.push({ step: STEPS[stage], kind: stage === 'ProcurementFulfillment' ? 'Fulfillment' : 'Approval', state: 'Planned' })
    }
  }
  return { ...request, step: mockStep(request.stage), route }
}
