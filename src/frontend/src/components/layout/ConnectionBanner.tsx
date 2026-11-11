import { useEffect, useRef } from 'react'
import { WifiOff } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toastContext'
import { useConnectivity } from '@/lib/connectivity/connectivityContext'
import type { ConnectionStatus } from '@/lib/connectivity/monitor'

const MESSAGES: Record<Exclude<ConnectionStatus, 'online'>, string> = {
  offline: "You're offline. Changes can't be saved until your connection is back.",
  serverUnreachable: "Can't reach FlowOps right now. Changes can't be saved until the connection is back.",
}
export const BACK_ONLINE_MESSAGE = "You're back online."

/**
 * App-wide strip shown while the server can't be reached (docs/UI_SPEC.md §2), on every page,
 * signed in or not. The live region is always mounted so the message is announced when it appears.
 */
export function ConnectionBanner() {
  const { status, checking, checkNow } = useConnectivity()
  const toast = useToast()
  const wasLost = useRef(false)

  useEffect(() => {
    if (status !== 'online') wasLost.current = true
    else if (wasLost.current) {
      wasLost.current = false
      toast.success(BACK_ONLINE_MESSAGE)
    }
  }, [status, toast])

  return (
    <div aria-live="polite" className="sticky top-0 z-40">
      {status !== 'online' && (
        <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-2 bg-warning-soft px-4 py-2 text-sm text-warning">
          <span className="flex items-start gap-2">
            <WifiOff aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
            <span>{MESSAGES[status]}</span>
          </span>
          <Button variant="secondary" size="sm" loading={checking} onClick={checkNow}>
            Try again
          </Button>
        </div>
      )}
    </div>
  )
}
