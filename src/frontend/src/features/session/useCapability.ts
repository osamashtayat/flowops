import type { Capability } from '@/lib/api/types'
import { useSession } from './api'

/**
 * Whether the server says the signed-in user has this capability. The UI decides what to show
 * from the capability list only — never from role names (docs/AUTHORIZATION.md §2). The API
 * remains the real enforcement.
 */
export function useCapability(capability: Capability): boolean {
  const { user } = useSession()
  return user?.capabilities.includes(capability) ?? false
}
