import { useSearchParams } from 'react-router'

export type UrlStateValue = string | number
type UrlStateValues = Record<string, UrlStateValue>

/**
 * Filters and page kept in the URL query string (docs/UI_SPEC.md: reload and shared links show the
 * same view). `defaults` fixes the keys and their types: a numeric default means "positive
 * integer", anything unparseable falls back to the default. Values equal to the default are left
 * out of the URL, so `/requests` stays clean. Pass a module-level constant for `defaults`.
 *
 * Changing any key except `page` sends `page` back to its default, because a different filter or
 * page size makes the old page number meaningless.
 */
export function useUrlState<T extends UrlStateValues>(defaults: T) {
  const [params, setParams] = useSearchParams()

  const state: UrlStateValues = {}
  for (const [key, fallback] of Object.entries(defaults)) {
    const raw = params.get(key)
    if (raw === null) {
      state[key] = fallback
    } else if (typeof fallback === 'number') {
      const parsed = /^\d+$/.test(raw) ? Number(raw) : Number.NaN
      state[key] = Number.isSafeInteger(parsed) && parsed >= 1 ? parsed : fallback
    } else {
      state[key] = raw
    }
  }

  function update(patch: Partial<T>) {
    setParams((previous) => {
      const next = new URLSearchParams(previous)
      for (const [key, value] of Object.entries(patch)) {
        if (!(key in defaults)) continue
        if (value === undefined || value === defaults[key]) next.delete(key)
        else next.set(key, String(value))
      }
      if ('page' in defaults && Object.keys(patch).some((key) => key !== 'page')) {
        if (!('page' in patch)) next.delete('page')
      }
      return next
    })
  }

  // Safe: `state` has exactly the keys and value types of `defaults` (built from them above).
  return [state as T, update] as const
}
