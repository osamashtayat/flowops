import { expect, test } from '@playwright/test'
import { dateFromToday, DEMO, inboxRow, signIn } from './demo'

test('leave: the manager rejects with a reason, and the requester sees it', async ({ browser }) => {
  const reason = 'Release week; please pick other dates'

  // Karim asks for leave.
  const karim = await signIn(browser, DEMO.karim)
  await karim.goto('/requests/new/leave')
  await karim.getByLabel(/^Start date/).fill(dateFromToday(40))
  await karim.getByLabel(/^End date/).fill(dateFromToday(42))
  await karim.getByLabel(/^Leave type/).selectOption('Annual')
  await karim.getByRole('button', { name: 'Submit request' }).click()
  const heading = karim.getByRole('heading', { level: 1, name: /^FO-\d{4}-\d{6} · Annual leave/ })
  await expect(heading).toBeVisible()
  const number = ((await heading.textContent()) ?? '').split(' · ')[0] ?? ''
  const requestUrl = karim.url()

  // Samir rejects from the inbox; a reason is required.
  const samir = await signIn(browser, DEMO.samir)
  const row = await inboxRow(samir, number)
  await expect(row).toContainText('3 days · Annual')
  await row.getByRole('button', { name: 'Reject' }).click()
  const dialog = samir.getByRole('dialog')
  await dialog.getByRole('button', { name: 'Reject request' }).click()
  await expect(dialog.getByText('Enter a reason.')).toBeVisible()
  await dialog.getByLabel(/^Reason/).fill(reason)
  await dialog.getByRole('button', { name: 'Reject request' }).click()
  await expect(samir.getByText(`${number} rejected.`)).toBeVisible()

  // Karim sees the rejection and why.
  await karim.goto(requestUrl)
  await expect(karim.getByText('Rejected', { exact: true }).first()).toBeVisible()
  await expect(karim.getByText(reason).first()).toBeVisible()
})
