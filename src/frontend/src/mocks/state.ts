import type { MockUserKey } from './users'
import { MOCK_USER_KEYS } from './users'

/**
 * Who the mock server thinks is signed in. Kept in localStorage so a reload (and the dev role
 * switcher) keeps the chosen user; `null` means signed out.
 */
const STORAGE_KEY = 'flowops.mock.user'
const DEFAULT_USER: MockUserKey = 'employee'
const SIGNED_OUT = 'signed-out'

let signedIn: MockUserKey | null = readStored()
let mustChangePasswordCleared = new Set<MockUserKey>()

function readStored(): MockUserKey | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (stored === SIGNED_OUT) return null
    return MOCK_USER_KEYS.find((key) => key === stored) ?? DEFAULT_USER
  } catch {
    return DEFAULT_USER
  }
}

function persist(value: MockUserKey | null) {
  try {
    window.localStorage.setItem(STORAGE_KEY, value ?? SIGNED_OUT)
  } catch {
    // Storage blocked: the in-memory value still works for this page load.
  }
}

export function getMockUserKey(): MockUserKey | null {
  return signedIn
}

export function setMockUser(key: MockUserKey | null) {
  signedIn = key
  persist(key)
}

/** A new hire stops being forced to change their password once they have changed it. */
export function hasChangedMockPassword(key: MockUserKey): boolean {
  return mustChangePasswordCleared.has(key)
}

export function markMockPasswordChanged(key: MockUserKey) {
  mustChangePasswordCleared.add(key)
}

/** Back to the default state; used between tests and by the dev "reset" action. */
export function resetMockState() {
  mustChangePasswordCleared = new Set()
  setMockUser(DEFAULT_USER)
}
