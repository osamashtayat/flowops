import { CenteredPage } from '@/components/layout/CenteredPage'
import { ErrorState } from '@/components/ui/ErrorState'
import { Skeleton } from '@/components/ui/Skeleton'
import { toUserMessage } from '@/lib/api/problem'

/** Shown while `GET /api/auth/me` is in flight on first load. */
export function SessionLoading() {
  return (
    <CenteredPage title="Loading">
      <output className="sr-only">Loading your session…</output>
      <div className="space-y-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    </CenteredPage>
  )
}

/**
 * The session could not be read (network or server problem — not "signed out").
 * Takes `error`/`onRetry` from the caller: mounting a second `useSession` observer here would
 * refetch the failed query on mount and bounce between this screen and the loading screen.
 */
export function SessionError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <CenteredPage title="Something went wrong">
      <ErrorState message={toUserMessage(error)} onRetry={onRetry} />
    </CenteredPage>
  )
}
