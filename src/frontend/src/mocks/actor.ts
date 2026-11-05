import { bypass } from 'msw'
import type { CurrentUserDto } from '@/lib/api/types'
import { getMockUserKey } from './state'
import { MOCK_USERS } from './users'

/**
 * Who is calling a mocked endpoint. Tests use the in-memory mock user (state.ts); in the browser
 * the sign-in is real, so the mocks ask the real `GET /api/auth/me` instead (`actAsRealSession`).
 */
type ActorResolver = () => Promise<CurrentUserDto | null>

const mockSessionActor: ActorResolver = () => {
  const key = getMockUserKey()
  return Promise.resolve(key ? MOCK_USERS[key] : null)
}

let resolver: ActorResolver = mockSessionActor

export function currentActor(): Promise<CurrentUserDto | null> {
  return resolver()
}

/** Browser dev mode: mocked data endpoints answer for whoever is signed in against the real API. */
export function actAsRealSession() {
  resolver = async () => {
    // `bypass` sends the request to the network instead of back into the handlers.
    const response = await fetch(bypass(new Request(new URL('/api/auth/me', window.location.origin))))
    return response.ok ? ((await response.json()) as CurrentUserDto) : null
  }
}
