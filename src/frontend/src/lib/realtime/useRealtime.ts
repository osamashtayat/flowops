import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { startRealtime } from './connection'

/**
 * Off where there is no hub to talk to: unit tests, and the dev server while the API is mocked
 * (VITE_API_MOCKS is not "off"). With the real API on, realtime is on.
 */
export const REALTIME_ENABLED =
  import.meta.env.MODE !== 'test' && (!import.meta.env.DEV || import.meta.env.VITE_API_MOCKS === 'off')

/** Keeps one hub connection open while the signed-in shell is mounted. */
export function useRealtime(enabled: boolean = REALTIME_ENABLED) {
  const queryClient = useQueryClient()
  useEffect(() => {
    if (!enabled) return
    return startRealtime(queryClient)
  }, [enabled, queryClient])
}
