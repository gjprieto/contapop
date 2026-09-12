import { expect, test } from '@playwright/test';
import path from 'node:path';

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

test('pilot can manage the Phase 3 billing critical path', async ({ page }) => {
  const suffix = Date.now().toString();
  const bankName = `Phase 3 Bank ${suffix}`;
  const accountNumber = `ES${suffix}`;
  const transactionDescription = `Phase 3 payment ${suffix}`;
  const counterparty = `Phase 3 Customer ${suffix}`;
  const initialDescription = `Initial consulting ${suffix}`;
  const updatedDescription = `Updated consulting ${suffix}`;
  const archiveCounterparty = `Phase 3 Archive ${suffix}`;
  const deleteCounterparty = `Phase 3 Delete ${suffix}`;
  const invoiceFile = path.join(import.meta.dirname, 'fixtures', 'phase-3-invoice.pdf');
  const otherFile = path.join(import.meta.dirname, 'fixtures', 'phase-3-other.pdf');

  await page.addInitScript(() => {
    window.open = (() => window) as typeof window.open;
  });
  await signIn(page);

  // Create the Phase 2-side record this test reconciles instead of depending on another spec's data.
  await page.getByRole('link', { name: 'Financial Overview' }).click();
  await page.getByRole('button', { name: 'Link bank account' }).click();
  await page.getByLabel('Bank name').fill(bankName);
  await page.getByLabel('Account number').fill(accountNumber);
  await page.getByRole('button', { name: 'Link account' }).click();
  await expect(page.getByRole('cell', { name: bankName })).toBeVisible();

  await page.getByRole('link', { name: 'Transactions' }).click();
  await page.getByRole('button', { name: 'Record transaction' }).click();
  await page.getByLabel('Bank account').selectOption({ label: `${bankName} · ${accountNumber}` });
  await page.getByLabel('Amount (EUR)').fill('121');
  await page.getByLabel('Date', { exact: true }).fill('2026-09-12');
  await page.getByLabel('Description').fill(transactionDescription);
  await page.getByRole('button', { name: 'Save transaction' }).click();
  await expect(page.getByText(transactionDescription)).toBeVisible();

  await page.getByRole('link', { name: 'Invoices' }).click();
  await page.getByRole('button', { name: 'Create invoice' }).click();
  await page.getByLabel('Or add a counterparty').fill(counterparty);
  await page.getByRole('button', { name: 'Add' }).click();
  await page.getByLabel('Line description').fill(initialDescription);
  await page.getByLabel('Unit price (EUR)').fill('100');
  await page.getByLabel('Invoice date').fill('2026-09-12');
  await page.getByLabel('Due date').fill('2026-10-12');
  await page.getByRole('button', { name: 'Add line' }).click();
  await page.getByLabel('Line description').nth(1).fill(`Additional work ${suffix}`);
  await page.getByLabel('Unit price (EUR)').nth(1).fill('10');
  await expect(page.getByText(/Total\s+133,10/)).toBeVisible();
  await page.getByRole('button', { name: 'Create draft' }).click();

  const invoiceRow = page.getByRole('row').filter({ hasText: counterparty });
  await expect(invoiceRow).toBeVisible();
  await page.getByLabel(`View invoice details for ${counterparty}`).click();
  await page.getByRole('link', { name: 'Edit draft' }).click();
  await page.getByLabel('Invoice date').fill('2026-09-13');
  await page.getByLabel('Due date').fill('2026-10-13');
  await page.getByLabel('Line description').first().fill(updatedDescription);
  await page.getByRole('button', { name: 'Remove line' }).click();
  await expect(page.getByText(/Total\s+121,00/)).toBeVisible();
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText(updatedDescription)).toBeVisible();

  await page.getByRole('link', { name: 'Back to invoices' }).click();
  await page.getByLabel('Search invoices').fill(counterparty);
  await expect(invoiceRow).toBeVisible();
  await page.getByLabel(`View invoice details for ${counterparty}`).click();

  await page.getByRole('button', { name: 'Upload attachment' }).click();
  await page.getByRole('dialog', { name: 'Choose attachment type' }).getByLabel('Other type of attachment').setInputFiles(otherFile);
  await expect(page.getByText('Other: phase-3-other.pdf')).toBeVisible();
  await page.getByRole('link', { name: 'Back to invoices' }).click();
  await expect(page.getByLabel(`Open invoice attachment for ${counterparty}`)).toHaveCount(0);
  await page.getByLabel(`Attach document for ${counterparty}`).click();
  await page.getByRole('dialog', { name: 'Choose attachment type' }).getByLabel('Invoice').setInputFiles(invoiceFile);
  await expect(page.getByLabel(`Open invoice attachment for ${counterparty}`)).toBeVisible();
  await page.getByLabel(`Open invoice attachment for ${counterparty}`).click();

  await page.getByLabel(`View invoice details for ${counterparty}`).click();
  await page.getByLabel('Upload other attachment').setInputFiles(otherFile);
  await expect(page.getByText('Other: phase-3-other.pdf')).toHaveCount(2);
  await page.getByRole('listitem').filter({ hasText: 'phase-3-invoice.pdf' }).getByRole('button', { name: 'Remove attachment' }).click();
  await page.getByRole('button', { name: 'Remove attachment' }).click();
  await expect(page.getByRole('dialog', { name: 'Remove attachment?' })).toBeVisible();
  await page.getByRole('button', { name: 'Remove attachment' }).click();
  await page.getByRole('button', { name: 'Upload attachment' }).click();
  await expect(page.getByRole('dialog', { name: 'Choose attachment type' })).toBeVisible();
  await page.getByRole('button', { name: 'Cancel' }).click();

  await page.getByRole('link', { name: 'Back to invoices' }).click();
  await page.getByRole('row').filter({ hasText: counterparty }).getByRole('button', { name: 'Issue' }).click();
  await page.getByRole('link', { name: 'Payments' }).click();
  await page.getByRole('button', { name: 'Record payment' }).click();
  await page.getByLabel('Invoice').selectOption({ label: counterparty });
  await page.getByLabel('Amount (EUR)').fill('121');
  await page.getByLabel('Date').fill('2026-09-13');
  await page.getByLabel('Payment method').fill('bank_transfer');
  await page.getByRole('button', { name: 'Save payment' }).click();

  await expect(async () => {
    const transaction = page.getByLabel(/Transaction for payment/);
    const transactionOption = transaction.getByRole('option', { name: new RegExp(transactionDescription) });
    await transaction.selectOption((await transactionOption.getAttribute('value'))!);
    await page.getByRole('button', { name: 'Reconcile' }).click();
    await expect(page.getByText('Reconciled')).toBeVisible();
  }).toPass({ timeout: 30_000 });

  await page.getByRole('link', { name: 'Invoices' }).click();
  await page.getByLabel('Search invoices').fill(counterparty);
  await expect(page.getByRole('row').filter({ hasText: counterparty })).toContainText('paid');
  await expect(page.getByRole('button', { name: 'Archive' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Clear filters' }).click();

  await page.getByRole('button', { name: 'Create invoice' }).click();
  await page.getByLabel('Or add a counterparty').fill(archiveCounterparty);
  await page.getByRole('button', { name: 'Add' }).click();
  await page.getByLabel('Line description').fill(`Archive invoice ${suffix}`);
  await page.getByLabel('Unit price (EUR)').fill('10');
  await page.getByLabel('Invoice date').fill('2026-09-12');
  await page.getByLabel('Due date').fill('2026-10-12');
  await page.getByRole('button', { name: 'Create draft' }).click();
  await page.getByRole('row').filter({ hasText: archiveCounterparty }).getByRole('button', { name: 'Issue' }).click();
  await page.getByRole('row').filter({ hasText: archiveCounterparty }).getByRole('button', { name: 'Archive' }).click();
  await page.getByRole('button', { name: 'Archive invoice' }).click();
  await expect(page.getByRole('row').filter({ hasText: archiveCounterparty })).toBeHidden();
  await page.getByLabel('Invoice status').selectOption('archived');
  await expect(page.getByRole('row').filter({ hasText: archiveCounterparty })).toBeVisible();
  await page.getByRole('button', { name: 'Clear filters' }).click();

  await page.getByRole('button', { name: 'Create invoice' }).click();
  await page.getByLabel('Or add a counterparty').fill(deleteCounterparty);
  await page.getByRole('button', { name: 'Add' }).click();
  await page.getByLabel('Line description').fill(`Accidental draft ${suffix}`);
  await page.getByLabel('Unit price (EUR)').fill('10');
  await page.getByLabel('Invoice date').fill('2026-09-12');
  await page.getByLabel('Due date').fill('2026-10-12');
  await page.getByRole('button', { name: 'Create draft' }).click();
  const deleteRow = page.getByRole('row').filter({ hasText: deleteCounterparty });
  await deleteRow.getByRole('button', { name: 'Delete' }).click();
  await page.getByRole('button', { name: 'Delete invoice' }).click();
  await expect(deleteRow).toBeHidden();
});
