import { setupServer } from 'msw/node'
import { handlers } from './handlers'

/** The same handlers as the browser, for Vitest (see src/test/setup.ts). */
export const server = setupServer(...handlers)
