import { setupWorker } from 'msw/browser'
import { actAsRealSession } from './actor'
import { pendingHandlers } from './handlers'

/**
 * Starts the mock API in the browser (dev only — see main.tsx). Sign-in is real (the auth
 * endpoints fall through to the API); only endpoints whose backend task isn't merged yet are mocked (see handlers.ts).
 * Requests the handlers don't cover also fall through to the real server.
 */
export async function startMockApi() {
  actAsRealSession()
  const worker = setupWorker(...pendingHandlers)
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true })
}
