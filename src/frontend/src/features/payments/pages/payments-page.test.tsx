import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { PaymentsPage } from './payments-page';
const mocks = vi.hoisted(() => ({ getPayments: vi.fn(), getInvoices: vi.fn(), getUnreconciledTransactions: vi.fn(), reconcilePayment: vi.fn() }));
vi.mock('../api/payments-api', () => ({ getPayments: mocks.getPayments, getUnreconciledTransactions: mocks.getUnreconciledTransactions, reconcilePayment: mocks.reconcilePayment, recordPayment: vi.fn() }));
vi.mock('../../invoices/api/invoices-api', () => ({ getInvoices: mocks.getInvoices }));
function renderPage() { mocks.getPayments.mockResolvedValue({ items: [{ paymentId: 'payment-1', invoiceId: 'invoice-1', amountMinor: 12100, date: '2026-09-09', paymentMethod: 'bank_transfer', version: 1 }], page: 1, pageSize: 100, totalCount: 1 }); mocks.getInvoices.mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 }); mocks.getUnreconciledTransactions.mockResolvedValue({ items: [{ transactionId: 'transaction-1', amountMinor: 12100, date: '2026-09-09', description: 'Acme transfer' }], page: 1, pageSize: 100, totalCount: 1 }); render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><PaymentsPage /></MemoryRouter></QueryClientProvider>); }
describe('PaymentsPage', () => { it('offers an unreconciled transaction and starts reconciliation without a claim id', async () => { const user = userEvent.setup(); renderPage(); const select = await screen.findByLabelText('Transaction for payment payment-1'); await user.selectOptions(select, 'payment-1:transaction-1'); await user.click(screen.getByRole('button', { name: 'Reconcile' })); expect(mocks.reconcilePayment).toHaveBeenCalledWith(expect.objectContaining({ paymentId: 'payment-1' }), 'transaction-1'); }); });
