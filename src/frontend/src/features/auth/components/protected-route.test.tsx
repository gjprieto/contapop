import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ProtectedRoute } from './protected-route';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ProtectedRoute', () => {
  it('redirects an unauthenticated visitor to login', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/private']}>
          <Routes>
            <Route path="/login" element={<p>Login page</p>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/private" element={<p>Private page</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Login page')).toBeVisible();
  });
});
