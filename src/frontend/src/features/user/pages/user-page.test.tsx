import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { UserPage } from './user-page';

const user = { userId: 'user-1', tenantId: 'tenant-1', projectId: 'project-1', name: 'Ana', email: 'ana@example.com', theme: 'light', language: 'es', notificationsEnabled: true, version: 1 };

vi.mock('../../auth/api/auth-api', () => ({
  updateUserProfile: vi.fn(async ({ name }: { name: string }) => ({ ...user, name, version: 2 })),
}));

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(['auth', 'current-user'], user);
  render(<QueryClientProvider client={queryClient}><UserPage /></QueryClientProvider>);
  return queryClient;
}

describe('UserPage', () => {
  it('validates the profile name', async () => {
    const actor = userEvent.setup();
    renderPage();
    await actor.clear(screen.getByLabelText('Name'));
    await actor.click(screen.getByRole('button', { name: 'Save profile' }));
    expect(await screen.findByText('Enter your name.')).toBeVisible();
  });

  it('updates the current-user cache after saving', async () => {
    const actor = userEvent.setup();
    const queryClient = renderPage();
    await actor.clear(screen.getByLabelText('Name'));
    await actor.type(screen.getByLabelText('Name'), 'Ana Garcia');
    await actor.click(screen.getByRole('button', { name: 'Save profile' }));
    expect(await screen.findByText('Profile saved.')).toBeVisible();
    expect(queryClient.getQueryData<typeof user>(['auth', 'current-user'])?.name).toBe('Ana Garcia');
  });
});
