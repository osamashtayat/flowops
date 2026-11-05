import type { CurrentUserDto } from '@/lib/api/types'

/**
 * Mock people for working without the backend (every endpoint still returns 501 until BE-05+).
 * Shapes are the generated `CurrentUserDto`; capabilities follow docs/AUTHORIZATION.md §2:
 * a manager is an employee with direct reports (`tasks.view`), officers get `tasks.view`,
 * an administrator has `admin.manage` and no process authority.
 */
export const MOCK_PASSWORD = 'mock-password-1234'
export const MOCK_LOCKED_OUT_EMAIL = 'locked@flowops.test'

export type MockUserKey = 'employee' | 'manager' | 'finance' | 'admin' | 'newHire'

const engineering = { id: '3f0c5a2e-0000-4000-8000-000000000001', name: 'Engineering' }
const manager = { id: '3f0c5a2e-0000-4000-8000-0000000000a2', displayName: 'Omar Khalil' }

export const MOCK_USERS: Record<MockUserKey, CurrentUserDto> = {
  employee: {
    id: '3f0c5a2e-0000-4000-8000-0000000000a1',
    email: 'employee@flowops.test',
    displayName: 'Layla Haddad',
    department: engineering,
    manager,
    roles: ['Employee'],
    capabilities: ['requests.submit'],
    mustChangePassword: false,
  },
  manager: {
    id: manager.id,
    email: 'manager@flowops.test',
    displayName: manager.displayName,
    department: engineering,
    manager: null,
    roles: ['Employee'],
    capabilities: ['requests.submit', 'tasks.view'],
    mustChangePassword: false,
  },
  finance: {
    id: '3f0c5a2e-0000-4000-8000-0000000000a3',
    email: 'finance@flowops.test',
    displayName: 'Rania Nasser',
    department: { id: '3f0c5a2e-0000-4000-8000-000000000002', name: 'Finance' },
    manager: null,
    roles: ['Employee', 'FinanceOfficer'],
    capabilities: ['requests.submit', 'tasks.view'],
    mustChangePassword: false,
  },
  admin: {
    id: '3f0c5a2e-0000-4000-8000-0000000000a4',
    email: 'admin@flowops.test',
    displayName: 'Sami Qasem',
    department: null,
    manager: null,
    roles: ['Administrator'],
    capabilities: ['admin.manage'],
    mustChangePassword: false,
  },
  newHire: {
    id: '3f0c5a2e-0000-4000-8000-0000000000a5',
    email: 'newhire@flowops.test',
    displayName: 'Nour Saleh',
    department: engineering,
    manager,
    roles: ['Employee'],
    capabilities: ['requests.submit'],
    mustChangePassword: true,
  },
}

export const MOCK_USER_KEYS = Object.keys(MOCK_USERS) as MockUserKey[]

export function mockUserByEmail(email: string): MockUserKey | undefined {
  const normalised = email.trim().toLowerCase()
  return MOCK_USER_KEYS.find((key) => MOCK_USERS[key].email === normalised)
}
