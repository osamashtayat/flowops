import { cn } from '@/lib/cn'

export type InboxView = 'Open' | 'Done'

const TABS: readonly { view: InboxView; label: string }[] = [
  { view: 'Open', label: 'Open' },
  { view: 'Done', label: 'Completed' },
]

export interface InboxTabsProps {
  value: InboxView
  onChange: (view: InboxView) => void
}

/** Open / Completed switch. Plain buttons with `aria-current` (it swaps one list, so no tab panels). */
export function InboxTabs({ value, onChange }: InboxTabsProps) {
  return (
    <nav aria-label="Inbox view" className="flex gap-1 border-b border-border">
      {TABS.map(({ view, label }) => (
        <button
          key={view}
          type="button"
          aria-current={value === view ? 'page' : undefined}
          onClick={() => onChange(view)}
          className={cn(
            '-mb-px h-10 border-b-2 px-4 text-sm font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
            value === view ? 'border-primary text-ink' : 'border-transparent text-ink-muted hover:text-ink',
          )}
        >
          {label}
        </button>
      ))}
    </nav>
  )
}
