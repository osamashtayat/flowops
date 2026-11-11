import { createContext, useContext, useSyncExternalStore } from 'react'
import type { ConnectivityMonitor, ConnectivitySnapshot } from './monitor'

export const ConnectivityContext = createContext<ConnectivityMonitor | null>(null)

export interface ConnectivityApi extends ConnectivitySnapshot {
  /** Checks the server now instead of waiting for the next automatic check. */
  checkNow: () => void
}

/** Whether the server can be reached right now (see `createConnectivityMonitor`). */
export function useConnectivity(): ConnectivityApi {
  const monitor = useContext(ConnectivityContext)
  if (!monitor) throw new Error('useConnectivity must be used inside <ConnectivityProvider>.')
  const snapshot = useSyncExternalStore(monitor.subscribe, monitor.getSnapshot)
  return { ...snapshot, checkNow: monitor.checkNow }
}
