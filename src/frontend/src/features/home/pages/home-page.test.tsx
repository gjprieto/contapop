import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { HomePage } from './home-page';

describe('HomePage', () => {
  it('greets the authenticated user', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(['auth', 'current-user'], { userId: 'user-1', tenantId: 'tenant-1', projectId: 'project-1', name: 'Ana', email: 'ana@example.com', theme: 'light', language: 'es', notificationsEnabled: true, version: 1 });
    render(<QueryClientProvider client={queryClient}><HomePage /></QueryClientProvider>);
    expect(screen.getByRole('heading', { name: 'Hello, Ana' })).toBeVisible();
  });
});
