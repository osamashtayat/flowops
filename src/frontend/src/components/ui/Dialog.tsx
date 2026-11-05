import { useEffect, useId, useRef, type KeyboardEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { cn } from '@/lib/cn'

export interface DialogProps {
  open: boolean
  /** Called for Esc, the close button and a backdrop click (all suppressed when `dismissible` is false). */
  onClose: () => void
  /** Names the dialog (`aria-labelledby`). State the action and object: "Reject FO-2026-000123?". */
  title: string
  /** Optional one-line summary announced with the title (`aria-describedby`). */
  description?: ReactNode
  /** 480 px "confirm" or 640 px "form" panel; both become a full-screen sheet below 640 px. */
  size?: 'confirm' | 'form'
  /**
   * `center` (default) is a modal in the middle of the screen. `start` is a full-height panel on
   * the leading edge — the mobile navigation drawer — with the same focus handling.
   */
  placement?: 'center' | 'start'
  /** Set false while a request is running, so the dialog cannot vanish mid-action. */
  dismissible?: boolean
  /** Buttons row, right-aligned. */
  footer?: ReactNode
  children?: ReactNode
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * Modal dialog: focus moves in on open (to the element marked `data-autofocus`, else the panel),
 * Tab/Shift+Tab stay inside, Esc closes, and focus returns to whatever opened it.
 * Content is only mounted while open, so children's state resets on every open.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  size = 'confirm',
  placement = 'center',
  dismissible = true,
  footer,
  children,
}: DialogProps) {
  const titleId = useId()
  const descriptionId = useId()
  const panelRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    if (!open) return
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const panel = panelRef.current
    const preferred = panel?.querySelector<HTMLElement>('[data-autofocus]')
    ;(preferred ?? panel)?.focus()

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
      opener?.focus()
    }
  }, [open])

  if (!open) return null

  function handleKeyDown(event: KeyboardEvent<HTMLDialogElement>) {
    if (event.key === 'Escape') {
      event.stopPropagation()
      if (dismissible) onClose()
      return
    }
    if (event.key !== 'Tab') return
    const panel = panelRef.current
    if (!panel) return
    const items = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE))
    const first = items[0]
    const last = items[items.length - 1]
    if (!first || !last) {
      event.preventDefault()
      return
    }
    const active = document.activeElement
    if (event.shiftKey && (active === first || active === panel)) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && active === last) {
      event.preventDefault()
      first.focus()
    }
  }

  return createPortal(
    // oxlint-disable-next-line jsx-a11y/no-static-element-interactions -- backdrop click is a pointer convenience; the keyboard paths are Esc and the Close button.
    <div
      className={cn(
        'fixed inset-0 z-40 flex bg-ink/40',
        placement === 'center' ? 'items-center justify-center p-4 max-sm:p-0' : 'items-stretch justify-start',
      )}
      onMouseDown={(event) => {
        if (dismissible && event.target === event.currentTarget) onClose()
      }}
    >
      <dialog
        ref={panelRef}
        open
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        onKeyDown={handleKeyDown}
        className={cn(
          // <dialog> is opened with the `open` attribute (not showModal) so we own focus and layering;
          // reset the UA's absolute positioning, border and padding.
          'static m-0 flex w-full flex-col border-0 p-0 text-ink shadow-overlay',
          placement === 'center'
            ? [
                'max-h-full rounded-lg bg-surface',
                size === 'confirm' ? 'max-w-120' : 'max-w-160',
                'max-sm:h-full max-sm:max-w-none max-sm:rounded-none',
              ].join(' ')
            : 'h-full max-w-72 rounded-none bg-sidebar',
        )}
      >
        <header className="flex items-start justify-between gap-4 px-5 pt-5">
          <h2 id={titleId} className="text-lg font-semibold text-ink">
            {title}
          </h2>
          {dismissible && (
            <button
              type="button"
              onClick={onClose}
              aria-label="Close dialog"
              className="-mr-2 -mt-1 inline-flex size-8 shrink-0 items-center justify-center rounded-sm text-ink-muted hover:bg-surface-muted"
            >
              <X aria-hidden="true" className="size-4" />
            </button>
          )}
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4 text-base text-ink">
          {description && (
            <p id={descriptionId} className="mb-4 text-ink-muted">
              {description}
            </p>
          )}
          {children}
        </div>
        {footer && <footer className="flex flex-wrap justify-end gap-3 px-5 pb-5">{footer}</footer>}
      </dialog>
    </div>,
    document.body,
  )
}
