/**
 * Shared look of text-like form controls (DESIGN_SYSTEM §5 "Inputs"). Outline uses
 * `border-border-strong` (3.6:1) — `border-border` is decorative only. Invalid state is driven by
 * `aria-invalid`, so appearance can never disagree with what assistive tech is told.
 */
export const controlClasses = [
  'block w-full rounded-sm border border-border-strong bg-surface px-3 text-base text-ink',
  'placeholder:text-ink-muted',
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
  // `:read-only` also matches <select> in browsers, so scope it to text-like controls.
  '[&:is(input,textarea):read-only]:bg-surface-muted',
  'disabled:cursor-not-allowed disabled:opacity-50',
  'aria-[invalid=true]:border-danger',
].join(' ')

export const singleLineControlClasses = `${controlClasses} h-10`
