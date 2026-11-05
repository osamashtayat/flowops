import { useId } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Select } from '@/components/ui/Select'
import { useSession } from '@/features/session/api'
import { setMockUser } from './state'
import { MOCK_USER_KEYS, MOCK_USER_LABELS, mockUserByEmail, type MockUserKey } from './users'

const SIGNED_OUT = 'signed-out'

/**
 * Development-only: pick who the mock API treats as signed in, to check each role's navigation
 * and screens without a backend. Not part of production builds (RootLayout loads it lazily,
 * behind `import.meta.env.DEV`).
 */
export default function MockUserSwitcher() {
  const id = useId()
  const queryClient = useQueryClient()
  const { user } = useSession()
  const current = user ? (mockUserByEmail(user.email) ?? SIGNED_OUT) : SIGNED_OUT

  function change(value: string) {
    setMockUser(value === SIGNED_OUT ? null : (MOCK_USER_KEYS.find((key) => key === value) ?? null))
    // Everything cached belongs to the previous user: refetch it all, session first.
    void queryClient.resetQueries()
  }

  return (
    <div className="fixed bottom-4 left-4 z-30 w-56 rounded-md border border-border bg-surface p-3 shadow-overlay">
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-ink-muted">
        Mock user (development only)
      </label>
      <Select id={id} value={current} onChange={(event) => change(event.target.value)}>
        {MOCK_USER_KEYS.map((key: MockUserKey) => (
          <option key={key} value={key}>
            {MOCK_USER_LABELS[key]}
          </option>
        ))}
        <option value={SIGNED_OUT}>Signed out</option>
      </Select>
    </div>
  )
}
