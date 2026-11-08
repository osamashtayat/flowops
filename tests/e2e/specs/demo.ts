import { expect, type Browser, type Page } from '@playwright/test'

/**
 * BE-16 demo accounts, as documented in the repository README ("Demo data"). The password is a
 * published development-only test value, not a secret.
 */
export const DEMO_PASSWORD = 'FlowOps-Demo-2026!'

export const DEMO = {
  lina: 'lina.dev@demo.flowops.test', // Engineering employee, manager Samir
  karim: 'karim.dev@demo.flowops.test', // Engineering employee, manager Samir
  samir: 'samir.manager@demo.flowops.test', // Engineering manager
  farah: 'farah.finance@demo.flowops.test', // the only finance officer
  yousef: 'yousef.procurement@demo.flowops.test', // procurement officer
} as const

/** A fresh browser context per person, so every session has its own cookies. */
export async function signIn(browser: Browser, email: string): Promise<Page> {
  const context = await browser.newContext()
  const page = await context.newPage()
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(DEMO_PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible()
  return page
}

/** Opens the inbox and returns the Open row for this request number. */
export async function inboxRow(page: Page, requestNumber: string) {
  await page.goto('/inbox?pageSize=100')
  const row = page.getByRole('table', { name: 'Open tasks' }).getByRole('row').filter({ hasText: requestNumber })
  await expect(row).toHaveCount(1)
  return row
}

/** "YYYY-MM-DD", `days` from today. Far enough ahead that the company time zone doesn't matter. */
export function dateFromToday(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000)
  return date.toISOString().slice(0, 10)
}

/** A suffix that keeps titles unique across runs against the same database. */
export const runId = Date.now().toString(36)
