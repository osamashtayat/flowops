/**
 * Date display. The API sends timestamps in UTC and dates as `yyyy-MM-dd`; users see them in
 * Asia/Amman as "5 Nov 2026" (docs/DESIGN_SYSTEM.md §6). Month names are assembled from
 * `formatToParts` so the result does not depend on a locale's abbreviation style ("Sept").
 */
export const DISPLAY_TIME_ZONE = 'Asia/Amman'

const dateParts = new Intl.DateTimeFormat('en-US', {
  timeZone: DISPLAY_TIME_ZONE,
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

const utcDateParts = new Intl.DateTimeFormat('en-US', {
  timeZone: 'UTC',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
})

function pick(formatter: Intl.DateTimeFormat, date: Date): Record<string, string> {
  const result: Record<string, string> = {}
  for (const part of formatter.formatToParts(date)) result[part.type] = part.value
  return result
}

/** A UTC timestamp as a calendar date in Amman: "2026-11-05T22:30:00Z" → "6 Nov 2026". */
export function formatDate(timestamp: string): string {
  const date = new Date(timestamp)
  if (Number.isNaN(date.getTime())) return timestamp
  const p = pick(dateParts, date)
  return `${p.day} ${p.month} ${p.year}`
}

/** A UTC timestamp with time in Amman: "2026-11-05T11:05:00Z" → "5 Nov 2026, 14:05". */
export function formatDateTime(timestamp: string): string {
  const date = new Date(timestamp)
  if (Number.isNaN(date.getTime())) return timestamp
  const p = pick(dateParts, date)
  return `${p.day} ${p.month} ${p.year}, ${p.hour}:${p.minute}`
}

/** A date-only value (`yyyy-MM-dd`, e.g. a leave date) — never shifted by a time zone. */
export function formatDateOnly(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return value
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])))
  if (Number.isNaN(date.getTime())) return value
  const p = pick(utcDateParts, date)
  return `${p.day} ${p.month} ${p.year}`
}

const calendarDay = new Intl.DateTimeFormat('en-CA', {
  timeZone: DISPLAY_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** Today's calendar date in Amman as `yyyy-MM-dd` — the company day the API checks "start ≥ today" against. */
export function todayInCompanyZone(now: Date = new Date()): string {
  const p = pick(calendarDay, now)
  return `${p.year}-${p.month}-${p.day}`
}

/** Calendar days from `start` to `end`, both included ("2026-11-12".."2026-11-14" → 3); null if either is not a date or end < start. */
export function calendarDaysInclusive(start: string, end: string): number | null {
  const parse = (value: string) => {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
    return match ? Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : Number.NaN
  }
  const days = (parse(end) - parse(start)) / 86_400_000 + 1
  return Number.isInteger(days) && days >= 1 ? days : null
}

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

/** "just now", "5 min ago", "3 h ago", "2 d ago"; older than 30 days falls back to the date (timelines, docs/DESIGN_SYSTEM.md §6). */
export function formatRelative(timestamp: string, now: Date = new Date()): string {
  const then = new Date(timestamp).getTime()
  if (Number.isNaN(then)) return timestamp
  const age = now.getTime() - then
  if (age < MINUTE) return 'just now'
  if (age < HOUR) return `${Math.floor(age / MINUTE)} min ago`
  if (age < DAY) return `${Math.floor(age / HOUR)} h ago`
  if (age < 30 * DAY) return `${Math.floor(age / DAY)} d ago`
  return formatDate(timestamp)
}

/** "in under a min", "in 5 min", "in 3 h", "in 2 d" for a future moment (role-change expiry); a moment already passed reads "now". */
export function formatTimeUntil(timestamp: string, now: Date = new Date()): string {
  const then = new Date(timestamp).getTime()
  if (Number.isNaN(then)) return timestamp
  const left = then - now.getTime()
  if (left <= 0) return 'now'
  if (left < MINUTE) return 'in under a min'
  if (left < HOUR) return `in ${Math.floor(left / MINUTE)} min`
  if (left < DAY) return `in ${Math.floor(left / HOUR)} h`
  return `in ${Math.floor(left / DAY)} d`
}
