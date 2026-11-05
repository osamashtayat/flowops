import { setupWorker } from 'msw/browser'
import { actAsRealSession } from './actor'
import { dataHandlers } from './handlers'

/**
 * Starts the mock API in the browser (dev only — see main.tsx). Sign-in is real (the auth
 * endpoints fall through to the API); only data endpoints without a merged backend are mocked.
 * Requests the handlers don't cover also fall through to the real server.
 */
export async function startMockApi() {
  actAsRealSession()
  const worker = setupWorker(...dataHandlers)
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true })
}
