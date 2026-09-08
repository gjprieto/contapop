import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { TransactionsPage } from './transactions-page';

const mocks = vi.hoisted(() => ({ getTransactions: vi.fn(), getBankAccounts: vi.fn(), importTransactions: vi.fn() }));

vi.mock('../api/transactions-api', () => ({
  getTransactions: mocks.getTransactions,
  createTransaction: vi.fn(),
  updateTransaction: vi.fn(),
  archiveTransaction: vi.fn(),
  importTransactions: mocks.importTransactions,
}));

vi.mock('../../accounts/api/accounts-api', () => ({ getBankAccounts: mocks.getBankAccounts, getPaymentCards: vi.fn() }));

function LocationDisplay() {
  const location = useLocation();
  return <output data-testid="location">{location.search}</output>;
}

function renderPage(initialEntry = '/transactions') {
  mocks.getTransactions.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 });
  mocks.getBankAccounts.mockResolvedValue({ items: [{ bankAccountId: 'account-1', bankName: 'Banco Uno', accountNumber: 'ES12', status: 'active', balanceMinor: 0, version: 1 }], page: 1, pageSize: 10, totalCount: 1 });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={[initialEntry]}><Routes><Route path="/transactions" element={<><TransactionsPage /><LocationDisplay /></>} /></Routes></MemoryRouter></QueryClientProvider>);
}

describe('TransactionsPage', () => {
  it('writes search and filters to URL state', async () => {
    const actor = userEvent.setup();
    renderPage();
    await actor.type(await screen.findByPlaceholderText('Search transactions...'), 'rent');
    await actor.selectOptions(screen.getByLabelText('Transaction type'), 'expense');
    expect(screen.getByTestId('location')).toHaveTextContent('search=rent');
    expect(screen.getByTestId('location')).toHaveTextContent('type=expense');
  });

  it('commits date filters to URL state when focus leaves the field', () => {
    renderPage('/transactions?dateFrom=2026-01-01');
    const fromDate = screen.getByLabelText('From date');
    expect(fromDate).toHaveValue('2026-01-01');

    fireEvent.change(fromDate, { target: { value: '2026-02-01' } });
    expect(screen.getByTestId('location')).toHaveTextContent('dateFrom=2026-01-01');

    fireEvent.blur(fromDate);
    expect(screen.getByTestId('location')).toHaveTextContent('dateFrom=2026-02-01');
  });

  it('moves through the CSV import wizard before submitting', async () => {
    const actor = userEvent.setup();
    renderPage();
    await actor.click(screen.getByRole('button', { name: 'Import CSV' }));
    expect(screen.getByRole('dialog', { name: 'Import transactions' })).toHaveTextContent('No file selected.');
    const file = new File(['Date,Amount\n2026-01-01,-12.50'], 'statement.csv', { type: 'text/csv' });
    await actor.upload(screen.getByLabelText('Choose a CSV or Excel file'), file);
    await actor.selectOptions(screen.getByLabelText('Bank account'), 'account-1');
    await actor.click(screen.getByRole('button', { name: 'Continue' }));
    expect(screen.getByLabelText('Date column')).toBeVisible();
    expect(screen.getByLabelText('Amount column')).toBeVisible();
  });

  it('shows the completion state after a CSV import succeeds', async () => {
    const actor = userEvent.setup();
    mocks.importTransactions.mockResolvedValue({ importedCount: 1, transactionIds: ['transaction-1'], skippedRows: [] });
    renderPage();

    await actor.click(screen.getByRole('button', { name: 'Import CSV' }));
    await actor.upload(screen.getByLabelText('Choose a CSV or Excel file'), new File(['Date,Amount\n2026-01-01,-12.50'], 'statement.csv', { type: 'text/csv' }));
    await actor.selectOptions(screen.getByLabelText('Bank account'), 'account-1');
    await actor.click(screen.getByRole('button', { name: 'Continue' }));
    await actor.type(screen.getByLabelText('Date column'), 'Date');
    await actor.type(screen.getByLabelText('Amount column'), 'Amount');
    await actor.click(screen.getByRole('button', { name: 'Import transactions' }));

    expect(await screen.findByText('Import complete')).toBeVisible();
    expect(mocks.importTransactions).toHaveBeenCalledWith(expect.objectContaining({ bankAccountId: 'account-1' }));
  });
});
