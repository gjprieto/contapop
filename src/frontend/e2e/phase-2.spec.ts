import { expect, test } from '@playwright/test';
import path from 'node:path';

const pilot = {
  email: process.env.PLAYWRIGHT_PILOT_EMAIL ?? 'pilot@example.com',
  password: process.env.PLAYWRIGHT_PILOT_PASSWORD ?? 'Password1!',
};

async function signIn(page: import('@playwright/test').Page) {
  await page.goto('/financial-overview');
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel('Email address').fill(pilot.email);
  await page.getByLabel('Password').fill(pilot.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/financial-overview$/);
}

test('pilot can manage an account and its transactions', async ({ page }) => {
  const suffix = Date.now().toString();
  const bankName = `Phase 2 Bank ${suffix}`;
  const accountNumber = `ES${suffix}`;
  const manualDescription = `Phase 2 manual transaction ${suffix}`;

  await signIn(page);
  await expect(page.getByText('No bank accounts linked yet.')).toBeVisible();

  // The form normally uses the pilot's replica-backed project ID. Replace it only
  // for this request to prove the UI surfaces the Ledger's fabricated-ID rejection.
  await page.route('**/experience/v1/financial-overview/bank-accounts', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }

    const request = route.request();
    const body = request.postDataJSON() as { accountNumber: string; bankName: string; projectId: string };
    await route.continue({ postData: JSON.stringify({ ...body, projectId: '00000000-0000-0000-0000-000000000001' }) });
  }, { times: 1 });

  await page.getByRole('button', { name: 'Link bank account' }).click();
  await page.getByLabel('Bank name').fill(bankName);
  await page.getByLabel('Account number').fill(accountNumber);
  const rejectedLink = page.waitForResponse((response) =>
    response.url().includes('/experience/v1/financial-overview/bank-accounts')
    && response.request().method() === 'POST'
    && response.status() === 422,
  );
  await page.getByRole('button', { name: 'Link account' }).click();
  await rejectedLink;
  await expect(page.getByRole('alert')).toContainText('We could not link this account.');

  const accountDialog = page.getByRole('dialog', { name: 'Link bank account' });
  await expect(async () => {
    await accountDialog.getByRole('button', { name: 'Link account' }).click();
    await expect(accountDialog).toBeHidden();
  }).toPass({ timeout: 30_000 });
  await expect(page.getByRole('dialog', { name: 'Link bank account' })).toBeHidden();
  await expect(page.getByRole('cell', { name: bankName })).toBeVisible();

  await page.getByRole('link', { name: 'Transactions' }).click();
  await page.getByRole('button', { name: 'Record transaction' }).click();
  await page.getByLabel('Bank account').selectOption({ label: `${bankName} · ${accountNumber}` });
  await page.getByLabel('Amount (EUR)').fill('12.34');
  await page.getByLabel('Date', { exact: true }).fill('2026-09-08');
  await page.getByLabel('Description').fill(manualDescription);
  await page.getByRole('button', { name: 'Save transaction' }).click();
  await expect(page.getByText(manualDescription)).toBeVisible();

  await page.getByRole('button', { name: 'Import CSV' }).click();
  await page.getByLabel('Choose a CSV or Excel file').setInputFiles(path.join(import.meta.dirname, 'fixtures', 'phase-2-transactions.csv'));
  await page.getByLabel('Bank account').selectOption({ label: `${bankName} · ${accountNumber}` });
  await page.getByRole('button', { name: 'Continue' }).click();
  await page.getByLabel('Date column').fill('Date');
  await page.getByLabel('Amount column').fill('Amount');
  await page.getByLabel('Type column (optional)').fill('Type');
  await page.getByLabel('Description column (optional)').fill('Description');
  await page.getByRole('button', { name: 'Import transactions' }).click();
  await expect(page.getByRole('status')).toContainText('Import complete');
  await page.getByRole('button', { name: 'Done' }).click();
  await expect(page.getByText('Phase 2 CSV import')).toBeVisible();

  const manualRow = page.getByRole('row').filter({ hasText: manualDescription });
  await manualRow.getByRole('button', { name: 'Archive' }).click();
  await expect(manualRow).toBeHidden();
  await page.getByLabel('Transaction status').selectOption('archived');
  await expect(page.getByRole('row').filter({ hasText: manualDescription })).toBeVisible();
});
