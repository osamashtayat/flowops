import { useEffect, useRef } from 'react'
import { useToast } from './toastContext'

export interface ClosedByRefreshState {
  /** A decision dialog owned by this component is open. */
  isOpen: boolean
  /** The user already sent the decision (pending, done or failed); its own outcome says what happened. */
  isSubmitted: boolean
  /** What to tell the user when the dialog goes away without their doing. */
  message: string
}

/**
 * A decision dialog lives inside the row or card it acts on. When a refetch (usually a realtime
 * event) removes that row, the dialog unmounts with it and whatever the user was typing is gone.
 * Call this in the component that owns the dialog: if it unmounts while the dialog is open, before
 * the user sent anything, and without a navigation (the URL is unchanged), the user gets an info
 * toast instead of a dialog that silently vanished (#67).
 *
 * The URL check keeps the back button and the session-ended redirect quiet; it also makes React's
 * development double mount harmless, since nothing is open at that point.
 */
export function useClosedByRefreshNotice(state: ClosedByRefreshState) {
  const toast = useToast()
  const latest = useRef({ ...state, href: '' })

  // After every commit: what the cleanup below should compare against.
  useEffect(() => {
    latest.current = { ...state, href: window.location.href }
  })

  useEffect(
    () => () => {
      const { isOpen, isSubmitted, message, href } = latest.current
      if (isOpen && !isSubmitted && href === window.location.href) toast.info(message)
    },
    [toast],
  )
}
