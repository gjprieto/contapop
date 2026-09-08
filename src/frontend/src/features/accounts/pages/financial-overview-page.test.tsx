import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FinancialOverviewPage } from './financial-overview-page';

const mocks = vi.hoisted(() => ({
  getBankAccounts: vi.fn(),
  getPaymentCards: vi.fn(),
  getCurrentUser: vi.fn(),
  createBankAccount: vi.fn(),
  createPaymentCard: vi.fn(),
}));

vi.mock('../api/accounts-api', () => ({
  getBankAccounts: mocks.getBankAccounts,
  getPaymentCards: mocks.getPaymentCards,
  createBankAccount: mocks.createBankAccount,
  createPaymentCard: mocks.createPaymentCard,
  archiveBankAccount: vi.fn(),
  removePaymentCard: vi.fn(),
}));

vi.mock('../../auth/api/auth-api', () => ({ getCurrentUser: mocks.getCurrentUser }));

function renderPage() {
  mocks.getCurrentUser.mockResolvedValue({ projectId: 'project-1' });
  mocks.getBankAccounts.mockResolvedValue({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  mocks.getPaymentCards.mockResolvedValue({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={queryClient}><FinancialOverviewPage /></QueryClientProvider>);
}

describe('FinancialOverviewPage', () => {
  it('submits a bank account against the current project', async () => {
    const actor = userEvent.setup();
    mocks.createBankAccount.mockResolvedValue({});
    renderPage();

    await actor.click(screen.getByRole('button', { name: 'Link bank account' }));
    await actor.type(screen.getByLabelText('Bank name'), 'Banco Uno');
    await actor.type(screen.getByLabelText('Account number'), 'ES12');
    await actor.click(screen.getByRole('button', { name: 'Link account' }));

    expect(mocks.createBankAccount).toHaveBeenCalledWith({ bankName: 'Banco Uno', accountNumber: 'ES12', projectId: 'project-1' });
  });

  it('validates required card-label fields before submitting', async () => {
    const actor = userEvent.setup();
    renderPage();

    await actor.click(screen.getByRole('button', { name: 'Add card label' }));
    await actor.click(screen.getByRole('button', { name: 'Add label' }));

    expect(await screen.findByText('Enter a card label.')).toBeVisible();
    expect(screen.getByText('Enter the cardholder name.')).toBeVisible();
    expect(mocks.createPaymentCard).not.toHaveBeenCalled();
  });
});
