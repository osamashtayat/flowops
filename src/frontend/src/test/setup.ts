import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/mocks/server'
import { resetMockAdmin } from '@/mocks/adminStore'
import { resetMockRequests } from '@/mocks/requestStore'
import { resetMockState } from '@/mocks/state'

// Unhandled requests fail the test: a component must never reach the network unexpectedly.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))

afterEach(() => {
  cleanup()
  server.resetHandlers()
  resetMockState()
  resetMockRequests()
  resetMockAdmin()
})

afterAll(() => server.close())
