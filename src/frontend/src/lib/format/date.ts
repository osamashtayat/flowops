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
