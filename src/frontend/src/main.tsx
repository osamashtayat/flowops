import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { AppProviders } from '@/app/providers'
import { router } from '@/app/router'
import '@/styles/theme.css'

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Root element #root not found')
const root = createRoot(rootElement)

/**
 * In development the API is mocked (MSW) until the backend implements the endpoints; set
 * VITE_API_MOCKS=off to talk to the real API. The dynamic import keeps the mocks out of
 * production builds.
 */
async function enableMocking() {
  if (!import.meta.env.DEV || import.meta.env.VITE_API_MOCKS === 'off') return
  try {
    const { startMockApi } = await import('@/mocks/browser')
    await startMockApi()
  } catch (error) {
    // e.g. a browser that blocks service workers: show the app anyway (it will reach the real API).
    console.error('Could not start the mock API; continuing without it.', error)
  }
}

void enableMocking().then(() => {
  root.render(
    <StrictMode>
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>
    </StrictMode>,
  )
})
