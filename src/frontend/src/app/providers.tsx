import { QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { ToastProvider } from '@/components/ui/Toast'
import { ConnectivityProvider } from '@/lib/connectivity/ConnectivityProvider'
import { createQueryClient } from '@/lib/queryClient'

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(createQueryClient)
  return (
    <QueryClientProvider client={queryClient}>
      <ConnectivityProvider>
        <ToastProvider>{children}</ToastProvider>
      </ConnectivityProvider>
    </QueryClientProvider>
  )
}
