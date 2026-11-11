import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { ConnectivityContext } from './connectivityContext'
import { createConnectivityMonitor, type ConnectivityOptions } from './monitor'

/** One connectivity monitor for the app; must sit inside `QueryClientProvider`. Options are read once. */
export function ConnectivityProvider({ children, options }: { children: ReactNode; options?: ConnectivityOptions }) {
  const queryClient = useQueryClient()
  const [monitor] = useState(() => createConnectivityMonitor(queryClient, options))
  useEffect(() => monitor.start(), [monitor])
  return <ConnectivityContext.Provider value={monitor}>{children}</ConnectivityContext.Provider>
}
