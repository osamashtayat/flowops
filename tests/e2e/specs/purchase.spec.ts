import { expect, test } from '@playwright/test'
import { DEMO, inboxRow, runId, signIn } from './demo'

test('purchase: employee → manager → finance → procurement, then completed for the requester', async ({ browser }) => {
  const item = `E2E monitor ${runId}`

  // Lina submits.
  const lina = await signIn(browser, DEMO.lina)
  await lina.goto('/requests/new/purchase')
  await lina.getByLabel(/^Item name/).fill(item)
  await lina.getByLabel(/^Description/).fill('27 inch, USB-C')
  await lina.getByLabel(/^Estimated cost/).fill('1234.5')
  await lina.getByLabel(/^Business justification/).fill('Second screen for code review')
  await lina.getByRole('button', { name: 'Submit request' }).click()
  const heading = lina.getByRole('heading', { level: 1, name: new RegExp(`^FO-\\d{4}-\\d{6} · ${item}$`) })
  await expect(heading).toBeVisible()
  const number = ((await heading.textContent()) ?? '').split(' · ')[0] ?? ''
  await expect(lina.getByText(`Request ${number} submitted.`)).toBeVisible()
  await expect(lina.getByText('Waiting for: Manager approval')).toBeVisible()
  const requestUrl = lina.url()

  // Samir (manager) approves from the inbox; the amount reads like everywhere else (#66).
  const samir = await signIn(browser, DEMO.samir)
  const samirRow = await inboxRow(samir, number)
  await expect(samirRow).toContainText('1,234.500 JOD')
  await samirRow.getByRole('button', { name: 'Approve' }).click()
  await samir.getByRole('dialog').getByRole('button', { name: 'Approve request' }).click()
  await expect(samir.getByText(`${number} approved.`)).toBeVisible()
  await expect(samir.getByRole('row').filter({ hasText: number })).toHaveCount(0)

  // Farah (finance) approves.
  const farah = await signIn(browser, DEMO.farah)
  const farahRow = await inboxRow(farah, number)
  await expect(farahRow).toContainText('Finance approval')
  await farahRow.getByRole('button', { name: 'Approve' }).click()
  await farah.getByRole('dialog').getByRole('button', { name: 'Approve request' }).click()
  await expect(farah.getByText(`${number} approved.`)).toBeVisible()

  // Yousef (procurement) fulfils.
  const yousef = await signIn(browser, DEMO.yousef)
  const yousefRow = await inboxRow(yousef, number)
  await yousefRow.getByRole('button', { name: 'Fulfill' }).click()
  await yousef.getByRole('dialog').getByRole('button', { name: 'Mark as fulfilled' }).click()
  await expect(yousef.getByText(`${number} marked as fulfilled.`)).toBeVisible()

  // Lina sees it completed, with every step in the history.
  await lina.goto(requestUrl)
  await expect(lina.getByText('Completed', { exact: true }).first()).toBeVisible()
  await expect(lina.getByText(/Samir.* approved the Manager approval step/)).toBeVisible()
  await expect(lina.getByText(/Farah.* approved the Finance approval step/)).toBeVisible()
  await expect(lina.getByText(/Yousef.* fulfilled the Procurement fulfilment step/)).toBeVisible()
})
