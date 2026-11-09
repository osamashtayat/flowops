import { defineConfig, devices } from '@playwright/test'

/**
 * QA-04 smoke against the OPS-01 compose stack seeded with the BE-16 demo company.
 * Start it first with `./stack.sh up` (see README.md in this folder).
 */
export default defineConfig({
  testDir: './specs',
  // The flows share one database and the same people; run them one after another.
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'https://localhost:9443',
    // nginx in the stack serves a self-signed certificate generated at start (OPS-01).
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
