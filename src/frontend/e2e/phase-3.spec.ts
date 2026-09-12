import { expect, test } from '@playwright/test';

const pilot = {
  email: process.env.PLAYWRIGHT_PILOT_EMAIL ?? 'pilot@example.com',
  password: process.env.PLAYWRIGHT_PILOT_PASSWORD ?? 'Password1!',
};

async function signIn(page: import('@playwright/test').Page) {
  await page.goto('/invoices');
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel('Email address').fill(pilot.email);
  await page.getByLabel('Password').fill(pilot.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/invoices$/);
}

test('pilot can permanently delete a separate accidental draft invoice', async ({ page }) => {
  const suffix = Date.now().toString();
  const counterparty = `Phase 3 Delete ${suffix}`;
  const description = `Accidental draft ${suffix}`;

  await signIn(page);
  await page.getByRole('button', { name: 'Create invoice' }).click();
  await page.getByLabel('Or add a counterparty').fill(counterparty);
  await page.getByRole('button', { name: 'Add' }).click();
  await page.getByLabel('Line description').fill(description);
  await page.getByLabel('Unit price (EUR)').fill('10');
  await page.getByLabel('Invoice date').fill('2026-09-12');
  await page.getByLabel('Due date').fill('2026-10-12');
  await page.getByRole('button', { name: 'Create draft' }).click();

  const row = page.getByRole('row').filter({ hasText: counterparty });
  await expect(row).toBeVisible();
  await row.getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByRole('dialog', { name: 'Delete draft invoice?' })).toBeVisible();
  await page.getByRole('button', { name: 'Delete invoice' }).click();
  await expect(row).toBeHidden();
  await expect(page.getByText(`${counterparty}`, { exact: true })).toBeHidden();
});
