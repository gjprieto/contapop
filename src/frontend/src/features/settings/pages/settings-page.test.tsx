import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SettingsPage } from './settings-page';

const user = { userId: 'user-1', tenantId: 'tenant-1', projectId: 'project-1', name: 'Ana', email: 'ana@example.com', theme: 'light', language: 'es', notificationsEnabled: true, version: 1 };

vi.mock('../../auth/api/auth-api', () => ({
  updateUserPreferences: vi.fn(async (preferences: object) => ({ ...user, ...preferences, version: 2 })),
}));

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(['auth', 'current-user'], user);
  render(<QueryClientProvider client={queryClient}><SettingsPage /></QueryClientProvider>);
  return queryClient;
}

describe('SettingsPage', () => {
  it('saves preferences and updates the current-user cache', async () => {
    const actor = userEvent.setup();
    const queryClient = renderPage();
    await actor.selectOptions(screen.getByLabelText('Theme'), 'dark');
    await actor.selectOptions(screen.getByLabelText('Language'), 'en');
    await actor.click(screen.getByLabelText('Enable notifications'));
    await actor.click(screen.getByRole('button', { name: 'Save preferences' }));
    expect(await screen.findByText('Preferences saved.')).toBeVisible();
    expect(queryClient.getQueryData<typeof user>(['auth', 'current-user'])).toMatchObject({ theme: 'dark', language: 'en', notificationsEnabled: false });
  });
});
