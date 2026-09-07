import { expect, test } from '@playwright/test';

const pilot = {
  email: process.env.PLAYWRIGHT_PILOT_EMAIL ?? 'pilot@example.com',
  password: process.env.PLAYWRIGHT_PILOT_PASSWORD ?? 'Password1!',
  name: process.env.PLAYWRIGHT_PILOT_NAME ?? 'Pilot User',
};

test('pilot can update profile and preferences which persist after reload', async ({ page }) => {
  const updatedName = `${pilot.name} Updated`;

  await page.goto('/');
  await expect(page).toHaveURL(/\/login$/);

  await page.getByLabel('Email address').fill(pilot.email);
  await page.getByLabel('Password').fill(pilot.password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: `Hello, ${pilot.name}` })).toBeVisible();

  await page.getByRole('link', { name: 'My Profile' }).click();
  await page.getByLabel('Name').fill(updatedName);
  await page.getByRole('button', { name: 'Save profile' }).click();
  await expect(page.getByRole('status')).toContainText('Profile saved.');

  await page.getByRole('link', { name: 'Settings' }).click();
  await page.getByLabel('Theme').selectOption('dark');
  await page.getByLabel('Language').selectOption('en');
  await page.getByLabel('Enable notifications').uncheck();
  await page.getByRole('button', { name: 'Save preferences' }).click();
  await expect(page.getByRole('status')).toContainText('Preferences saved.');

  await page.reload();
  await expect(page.getByLabel('Theme')).toHaveValue('dark');
  await expect(page.getByLabel('Language')).toHaveValue('en');
  await expect(page.getByLabel('Enable notifications')).not.toBeChecked();

  await page.getByRole('link', { name: 'My Profile' }).click();
  await expect(page.getByLabel('Name')).toHaveValue(updatedName);

  await page.getByLabel('Name').fill(pilot.name);
  await page.getByRole('button', { name: 'Save profile' }).click();
  await expect(page.getByRole('status')).toContainText('Profile saved.');

  await page.getByRole('link', { name: 'Settings' }).click();
  await page.getByLabel('Theme').selectOption('light');
  await page.getByLabel('Language').selectOption('es');
  await page.getByLabel('Enable notifications').check();
  await page.getByRole('button', { name: 'Save preferences' }).click();
  await expect(page.getByRole('status')).toContainText('Preferences saved.');
});
