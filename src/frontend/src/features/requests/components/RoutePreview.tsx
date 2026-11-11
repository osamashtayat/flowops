import { Fragment } from 'react'
import { ArrowRight } from 'lucide-react'
import { Skeleton } from '@/components/ui/Skeleton'
import type { RequestType, RouteFactsInput } from '@/lib/api/types'
import { SEARCH_DEBOUNCE_MS } from '@/lib/useDebouncedUrlSearch'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { useRoutePreview } from '../api'

/**
 * "What happens next" under a request form (docs/UI_SPEC.md §7.2): the route the server says these
 * facts take, skipped steps included. Hidden while the facts are not valid; a failure says so and never
 * blocks submitting. The route itself is the server's; nothing here decides which steps run.
 */
export function RoutePreview({ type, facts }: { type: RequestType; facts: RouteFactsInput | null }) {
  // Debounce the serialised facts, so a new object with the same content does not restart the timer.
  const key = useDebouncedValue(facts === null ? null : JSON.stringify(facts), SEARCH_DEBOUNCE_MS)
  const query = useRoutePreview(type, key === null ? null : (JSON.parse(key) as RouteFactsInput))
  if (key === null) return null

  return (
    <section aria-labelledby="route-preview-heading" aria-live="polite" className="rounded-md border border-border bg-surface-muted p-4">
      <h2 id="route-preview-heading" className="text-sm font-semibold text-ink">
        What happens next
      </h2>
      {query.isError ? (
        <p className="mt-2 text-sm text-ink-muted">Route preview unavailable</p>
      ) : query.data ? (
        <ol className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
          {query.data.steps.map((entry, index) => (
            <Fragment key={entry.step.key}>
              {index > 0 && <ArrowRight aria-hidden="true" className="size-4 text-ink-muted" />}
              <li className={entry.state === 'Skipped' ? 'text-ink-muted line-through' : 'text-ink'}>
                {entry.step.name}
                {entry.state === 'Skipped' && <span className="sr-only"> (skipped)</span>}
              </li>
            </Fragment>
          ))}
        </ol>
      ) : (
        <Skeleton className="mt-2 h-5 w-2/3" />
      )}
    </section>
  )
}
