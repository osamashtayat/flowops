import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { ToastProvider } from '@/components/ui/Toast'
import { ConnectivityProvider } from '@/lib/connectivity/ConnectivityProvider'
import type { ConnectivityOptions } from '@/lib/connectivity/monitor'
import { createQueryClient } from '@/lib/queryClient'

/**
 * Renders a route table the way the app does (query client, toasts, router) starting at
 * `initialPath`. Retries are instant so error states show without waiting for backoff.
 * `connectivity` overrides the connectivity monitor's options (e.g. its health-check delays).
 */
export function renderRoutes(routes: RouteObject[], initialPath: string, connectivity?: ConnectivityOptions) {
  const queryClient = createQueryClient()
  const defaults = queryClient.getDefaultOptions()
  queryClient.setDefaultOptions({ ...defaults, queries: { ...defaults.queries, retryDelay: 0 } })
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] })
  render(
    <QueryClientProvider client={queryClient}>
      <ConnectivityProvider options={connectivity}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </ConnectivityProvider>
    </QueryClientProvider>,
  )
  return { router, queryClient }
}
