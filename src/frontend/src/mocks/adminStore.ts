import type {
  CreateDepartmentRequest,
  CreateUserRequest,
  DepartmentDto,
  PagedStalledTasks,
  Role,
  StalledTaskDto,
  UpdateDepartmentRequest,
  UpdateUserRequest,
  UserDto,
} from '@/lib/api/types'
import { MOCK_USERS, type MockUserKey } from './users'

/**
 * In-memory users, departments and stalled tasks for the mock API, reset between tests. It
 * imitates only the rules the screens react to (docs/API_CONTRACT.md §8): duplicate email/code,
 * the last administrator, manager cycles, and no self-administration. The real rules are the backend's.
 */
export const MOCK_DEPARTMENTS = {
  engineering: { id: '3f0c5a2e-0000-4000-8000-000000000001', name: 'Engineering' },
  finance: { id: '3f0c5a2e-0000-4000-8000-000000000002', name: 'Finance' },
  legacy: { id: '3f0c5a2e-0000-4000-8000-000000000003', name: 'Legacy Projects' },
} as const

/** A failure the handler turns into a problem response. */
export interface AdminFailure {
  status: 400 | 404 | 409 | 422
  code: string
  field?: string
}

let departments: DepartmentDto[] = []
let users: UserDto[] = []

function userFromMock(key: MockUserKey): UserDto {
  const person = MOCK_USERS[key]
  return {
    id: person.id,
    email: person.email,
    displayName: person.displayName,
    isActive: true,
    mustChangePassword: person.mustChangePassword,
    department: person.department,
    manager: person.manager,
    roles: [...person.roles],
    directReportCount: 0,
    createdAt: '2026-01-05T08:00:00Z',
  }
}

export function resetMockAdmin() {
  departments = [
    { ...MOCK_DEPARTMENTS.engineering, code: 'ENG', isActive: true, memberCount: 0 },
    { ...MOCK_DEPARTMENTS.finance, code: 'FIN', isActive: true, memberCount: 0 },
    { ...MOCK_DEPARTMENTS.legacy, code: 'LEG', isActive: false, memberCount: 0 },
  ]
  const generated: UserDto[] = Array.from({ length: 25 }, (_, index) => ({
    ...userFromMock('employee'),
    id: `5e1f0000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
    email: `person${index + 1}@flowops.test`,
    displayName: `Person ${String(index + 1).padStart(2, '0')}`,
    isActive: index !== 24,
    roles: ['Employee'],
  }))
  users = [...(['employee', 'manager', 'finance', 'admin', 'newHire'] as const).map(userFromMock), ...generated]
  recount()
}

function recount() {
  departments = departments.map((d) => ({ ...d, memberCount: users.filter((u) => u.isActive && u.department?.id === d.id).length }))
  users = users.map((u) => ({ ...u, directReportCount: users.filter((o) => o.isActive && o.manager?.id === u.id).length }))
}

resetMockAdmin()

export const listMockDepartments = (includeInactive: boolean) =>
  [...departments].filter((d) => includeInactive || d.isActive).sort((a, b) => a.name.localeCompare(b.name))

export const getMockUser = (id: string) => users.find((u) => u.id === id)

export function listMockUsers(query: { search: string; departmentId: string; role: string; includeInactive: boolean; page: number; pageSize: number }) {
  const needle = query.search.toLowerCase()
  const matches = users
    .filter((u) => query.includeInactive || u.isActive)
    .filter((u) => !needle || u.displayName.toLowerCase().includes(needle) || u.email.toLowerCase().includes(needle))
    .filter((u) => !query.departmentId || u.department?.id === query.departmentId)
    .filter((u) => !query.role || u.roles.some((r) => r === query.role))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
  const start = (query.page - 1) * query.pageSize
  return { items: matches.slice(start, start + query.pageSize), page: query.page, pageSize: query.pageSize, totalCount: matches.length }
}

function managerRef(id: string | null | undefined, actorId: string, subjectId: string | null): UserDto['manager'] | AdminFailure {
  if (!id) return null
  const manager = users.find((u) => u.id === id)
  if (!manager || !manager.isActive) return { status: 400, code: 'validation', field: 'managerId' }
  if (manager.id === actorId) return { status: 422, code: 'user.self_administration' }
  if (subjectId) {
    for (let cursor: UserDto | undefined = manager; cursor; cursor = users.find((u) => u.id === cursor?.manager?.id)) {
      if (cursor.id === subjectId) return { status: 422, code: 'user.manager_cycle' }
    }
  }
  return { id: manager.id, displayName: manager.displayName }
}

export const isAdminFailure = (value: unknown): value is AdminFailure =>
  typeof value === 'object' && value !== null && 'status' in value && 'code' in value
const isFailure = isAdminFailure

function departmentRef(id: string | null | undefined): UserDto['department'] | AdminFailure {
  if (!id) return null
  const department = departments.find((d) => d.id === id)
  if (!department) return { status: 400, code: 'validation', field: 'departmentId' }
  if (!department.isActive) return { status: 422, code: 'department.inactive' }
  return { id: department.id, name: department.name }
}

export function createMockUser(actorId: string, body: CreateUserRequest): UserDto | AdminFailure {
  if (users.some((u) => u.email.toLowerCase() === body.email.toLowerCase())) return { status: 409, code: 'user.duplicate_email' }
  const department = departmentRef(body.departmentId)
  if (isFailure(department)) return department
  const manager = managerRef(body.managerId, actorId, null)
  if (isFailure(manager)) return manager
  const user: UserDto = {
    id: `5e1f0000-0000-4000-8000-${String(900 + users.length).padStart(12, '0')}`,
    email: body.email,
    displayName: body.displayName,
    isActive: true,
    mustChangePassword: true,
    department,
    manager,
    roles: [...new Set(body.roles)],
    directReportCount: 0,
    createdAt: new Date().toISOString(),
  }
  users = [...users, user]
  recount()
  return user
}

const activeAdmins = () => users.filter((u) => u.isActive && u.roles.includes('Administrator'))

export function updateMockUser(actorId: string, id: string, body: UpdateUserRequest): UserDto | AdminFailure {
  const current = getMockUser(id)
  if (!current) return { status: 404, code: 'not_found' }
  if (!body.isActive && current.roles.includes('Administrator') && activeAdmins().length === 1 && activeAdmins()[0]?.id === id) {
    return { status: 422, code: 'user.last_admin' }
  }
  const managerChanged = (body.managerId ?? null) !== (current.manager?.id ?? null)
  const departmentChanged = (body.departmentId ?? null) !== (current.department?.id ?? null)
  if (managerChanged && id === actorId) return { status: 422, code: 'user.self_administration' }
  const department = departmentChanged ? departmentRef(body.departmentId) : current.department
  if (isFailure(department)) return department
  const manager = managerChanged ? managerRef(body.managerId, actorId, id) : current.manager
  if (isFailure(manager)) return manager
  const updated: UserDto = { ...current, displayName: body.displayName, isActive: body.isActive, department, manager }
  users = users.map((u) => (u.id === id ? updated : u))
  recount()
  return getMockUser(id) ?? updated
}

export function updateMockRoles(actorId: string, id: string, roles: Role[]): UserDto | AdminFailure {
  const current = getMockUser(id)
  if (!current) return { status: 404, code: 'not_found' }
  const next = [...new Set(roles)]
  if (current.roles.includes('Administrator') && !next.includes('Administrator') && activeAdmins().length === 1 && current.isActive) {
    return { status: 422, code: 'user.last_admin' }
  }
  if (id === actorId) return { status: 422, code: 'user.self_administration' }
  users = users.map((u) => (u.id === id ? { ...u, roles: next } : u))
  return getMockUser(id) as UserDto
}

export function createMockDepartment(body: CreateDepartmentRequest): DepartmentDto | AdminFailure {
  const code = body.code.toUpperCase()
  if (departments.some((d) => d.code === code || d.name.toLowerCase() === body.name.toLowerCase())) return { status: 409, code: 'department.duplicate' }
  const created: DepartmentDto = { id: `3f0c5a2e-0000-4000-8000-${String(100 + departments.length).padStart(12, '0')}`, name: body.name, code, isActive: true, memberCount: 0 }
  departments = [...departments, created]
  return created
}

export function updateMockDepartment(id: string, body: UpdateDepartmentRequest): DepartmentDto | AdminFailure {
  const current = departments.find((d) => d.id === id)
  if (!current) return { status: 404, code: 'not_found' }
  const code = body.code.toUpperCase()
  if (departments.some((d) => d.id !== id && (d.code === code || d.name.toLowerCase() === body.name.toLowerCase()))) return { status: 409, code: 'department.duplicate' }
  const updated = { ...current, name: body.name, code, isActive: body.isActive }
  departments = departments.map((d) => (d.id === id ? updated : d))
  users = users.map((u) => (u.department?.id === id ? { ...u, department: { id, name: body.name } } : u))
  recount()
  return updated
}

export function resetMockPassword(id: string): AdminFailure | null {
  return getMockUser(id) ? null : { status: 404, code: 'not_found' }
}

const STALLED: StalledTaskDto[] = [
  {
    taskId: '7a1e0000-0000-4000-8000-0000000000f1',
    requestId: '9b2f0000-0000-4000-8000-0000000000f1',
    requestNumber: 'FO-2026-000201',
    requestType: 'EquipmentPurchase',
    stage: 'FinanceApproval',
    assignee: { user: null, role: 'FinanceOfficer' },
    reason: 'NoEligibleRoleMember',
    openSince: '2026-10-28T09:00:00Z',
  },
  {
    taskId: '7a1e0000-0000-4000-8000-0000000000f2',
    requestId: '9b2f0000-0000-4000-8000-0000000000f2',
    requestNumber: 'FO-2026-000202',
    requestType: 'Leave',
    stage: 'ManagerApproval',
    assignee: { user: { id: MOCK_USERS.manager.id, displayName: MOCK_USERS.manager.displayName }, role: null },
    reason: 'AssigneeInactive',
    openSince: '2026-11-01T09:00:00Z',
  },
]

export function listMockStalledTasks(page: number, pageSize: number): PagedStalledTasks {
  return { items: STALLED.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: STALLED.length }
}
