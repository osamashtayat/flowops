/**
 * Development only (see main.tsx): shown when the mock API could not start, e.g. a browser that
 * blocks service workers. Without it, screens whose backend task isn't built yet fail with a
 * generic message and the cause is hidden in the console.
 */
export function MockApiFailedBanner() {
  return (
    <p role="alert" className="fixed inset-x-0 top-0 z-50 bg-warning-soft px-4 py-2 text-center text-sm text-warning">
      Dev only: the mock API could not start (service workers blocked?). Screens whose backend isn&apos;t built yet will show errors. Use another
      browser, or see the console.
    </p>
  )
}
