import type { RequestDetailsDto, RequestType, RouteStepDto, StepRef } from '@/lib/api/types'

/**
 * The mock's stand-in for the server's steps and route (docs/API_CONTRACT.md §4, ADR-0014 Q2/Q5).
 * Imitated coarsely so screens can be exercised; the real order and names come from the backend (`StepRef`, `RequestRoute`).
 */
export const MOCK_STEPS = {
  manager: { key: 'manager-approval', name: 'Manager approval' },
  finance: { key: 'finance-approval', name: 'Finance approval' },
  hr: { key: 'hr-confirmation', name: 'HR confirmation' },
  procurement: { key: 'procurement-fulfillment', name: 'Procurement fulfilment' },
} as const satisfies Record<string, StepRef>

const ORDER: Record<RequestType, StepRef[]> = {
  EquipmentPurchase: [MOCK_STEPS.manager, MOCK_STEPS.finance, MOCK_STEPS.procurement],
  Leave: [MOCK_STEPS.manager, MOCK_STEPS.hr],
}

/** The planned route for a new request of `type`, every step `Planned` (the shape of `POST /api/requests/route-preview`). */
export const mockPreviewRoute = (type: RequestType): RouteStepDto[] =>
  ORDER[type].map((step) => ({ step, kind: mockStepKind(step), state: 'Planned' }))

export const mockStepKind = (step: StepRef): 'Fulfillment' | 'Approval' => (step.key === MOCK_STEPS.procurement.key ? 'Fulfillment' : 'Approval')

/** Recomputes the derived `route` from the stored tasks, as the server would on every read. */
export function withMockRoute(request: RequestDetailsDto): RequestDetailsDto {
  const route: RequestDetailsDto['route'] = request.tasks.map((task) => ({
    step: task.step,
    kind: task.kind,
    state: task.status === 'Open' ? 'Current' : 'Done',
  }))
  const open = request.tasks.find((task) => task.status === 'Open')
  if (open) {
    const order = ORDER[request.type]
    const index = order.findIndex((step) => step.key === open.step.key)
    for (const step of order.slice(index + 1)) route.push({ step, kind: mockStepKind(step), state: 'Planned' })
  }
  return { ...request, step: open?.step ?? null, route }
}
