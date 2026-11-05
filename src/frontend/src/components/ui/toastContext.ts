import { createContext, useContext } from 'react'

export type ToastTone = 'success' | 'error' | 'info'

export interface ToastApi {
  /** Auto-dismisses after 5 s (announced politely). */
  success: (message: string) => void
  /** Stays until dismissed (announced assertively). */
  error: (message: string) => void
  /** Auto-dismisses after 5 s (announced politely). */
  info: (message: string) => void
}

export const ToastContext = createContext<ToastApi | null>(null)

export function useToast(): ToastApi {
  const api = useContext(ToastContext)
  if (!api) throw new Error('useToast must be used inside <ToastProvider>.')
  return api
}
