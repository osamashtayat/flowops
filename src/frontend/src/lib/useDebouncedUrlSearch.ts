import { useEffect, useState } from 'react'

/** How long typing must pause before the search reaches the URL (docs/UI_SPEC.md §4.3). */
export const SEARCH_DEBOUNCE_MS = 300

/**
 * A search box whose committed value lives in the URL. `value` is what to show: the text being
 * typed, or the URL's value when the URL changed on its own (Clear filters, back button).
 * `commit` is called with the trimmed text once typing pauses.
 */
export function useDebouncedUrlSearch(urlValue: string, commit: (value: string) => void) {
  // `typed` is what the user has typed since the URL last changed; null means "show the URL's value".
  const [typed, setTyped] = useState<string | null>(null)
  const [seenUrlValue, setSeenUrlValue] = useState(urlValue)
  if (seenUrlValue !== urlValue) {
    setSeenUrlValue(urlValue)
    setTyped(null)
  }

  useEffect(() => {
    if (typed === null || typed.trim() === urlValue) return
    const timer = setTimeout(() => commit(typed.trim()), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [typed, urlValue, commit])

  return { value: typed ?? urlValue, onChange: setTyped }
}
