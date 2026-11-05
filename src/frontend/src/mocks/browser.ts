import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'

/**
 * Starts the mock API in the browser (dev only — see main.tsx). Requests the handlers don't cover
 * fall through to the real server.
 */
export async function startMockApi() {
  const worker = setupWorker(...handlers)
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true })
}
