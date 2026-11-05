import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { CircleCheck, CircleX, Info, X, type LucideIcon } from 'lucide-react'
import { ToastContext, type ToastApi, type ToastTone } from './toastContext'

const AUTO_DISMISS_MS = 5000

interface ToastItem {
  id: number
  tone: ToastTone
  message: string
}

const toneStyles: Record<ToastTone, { icon: LucideIcon; iconClass: string }> = {
  success: { icon: CircleCheck, iconClass: 'text-success' },
  error: { icon: CircleX, iconClass: 'text-danger' },
  info: { icon: Info, iconClass: 'text-info' },
}

/**
 * Bottom-right toast stack. Success/info toasts auto-dismiss after 5 s (the timer pauses while
 * hovered or focused); errors stay until dismissed. The two live regions are always mounted, so
 * screen readers announce new toasts reliably: `role="status"` (polite) and `role="alert"`.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(0)

  const api = useMemo<ToastApi>(() => {
    const push = (tone: ToastTone) => (message: string) =>
      setToasts((current) => [...current, { id: nextId.current++, tone, message }])
    return { success: push('success'), error: push('error'), info: push('info') }
  }, [])

  const dismiss = (id: number) => setToasts((current) => current.filter((toast) => toast.id !== id))
  const render = (items: ToastItem[]) => items.map((toast) => <ToastCard key={toast.id} toast={toast} onDismiss={dismiss} />)

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-50 flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2 max-sm:left-4 max-sm:w-auto">
        <output className="flex flex-col gap-2">{render(toasts.filter((toast) => toast.tone !== 'error'))}</output>
        <div role="alert" className="flex flex-col gap-2">
          {render(toasts.filter((toast) => toast.tone === 'error'))}
        </div>
      </div>
    </ToastContext.Provider>
  )
}

function ToastCard({ toast, onDismiss }: { toast: ToastItem; onDismiss: (id: number) => void }) {
  const [paused, setPaused] = useState(false)
  const { icon: Icon, iconClass } = toneStyles[toast.tone]
  const autoDismiss = toast.tone !== 'error'

  useEffect(() => {
    if (!autoDismiss || paused) return
    const timer = window.setTimeout(() => onDismiss(toast.id), AUTO_DISMISS_MS)
    return () => window.clearTimeout(timer)
  }, [autoDismiss, paused, onDismiss, toast.id])

  return (
    <div
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={() => setPaused(false)}
      className="pointer-events-auto flex items-start gap-3 rounded-md border border-border bg-surface p-4 shadow-overlay"
    >
      <Icon aria-hidden="true" className={`mt-0.5 size-5 shrink-0 ${iconClass}`} />
      <p className="min-w-0 flex-1 text-sm text-ink">{toast.message}</p>
      <button
        type="button"
        onClick={() => onDismiss(toast.id)}
        aria-label="Dismiss notification"
        className="-mr-2 -mt-1 inline-flex size-8 shrink-0 items-center justify-center rounded-sm text-ink-muted hover:bg-surface-muted"
      >
        <X aria-hidden="true" className="size-4" />
      </button>
    </div>
  )
}
