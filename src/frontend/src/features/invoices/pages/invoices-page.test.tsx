import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { InvoicesPage } from './invoices-page';

const mocks = vi.hoisted(() => ({ getInvoices: vi.fn(), getCounterparties: vi.fn(), getCurrentUser: vi.fn(), createInvoice: vi.fn() }));
vi.mock('../api/invoices-api', () => ({ getInvoices: mocks.getInvoices, getCounterparties: mocks.getCounterparties, createInvoice: mocks.createInvoice, createCounterparty: vi.fn(), changeInvoiceStatus: vi.fn(), downloadInvoice: vi.fn() }));
vi.mock('../../auth/api/auth-api', () => ({ getCurrentUser: mocks.getCurrentUser }));

function renderPage() { mocks.getInvoices.mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 }); mocks.getCounterparties.mockResolvedValue({ items: [{ counterpartyId: 'counterparty-1', type: 'customer', name: 'Acme SL', status: 'active' }], page: 1, pageSize: 100, totalCount: 1 }); mocks.getCurrentUser.mockResolvedValue({ projectId: 'project-1' }); render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><InvoicesPage /></MemoryRouter></QueryClientProvider>); }
describe('InvoicesPage', () => { it('shows calculated VAT and submits the derived minor-unit line', async () => { const user = userEvent.setup(); renderPage(); await user.click(screen.getByRole('button', { name: 'Create invoice' })); await user.selectOptions(await screen.findByLabelText('Counterparty'), 'counterparty-1'); await user.type(screen.getByLabelText('Line description'), 'Consulting'); await user.clear(screen.getByLabelText('Unit price (EUR)')); await user.type(screen.getByLabelText('Unit price (EUR)'), '100'); await user.type(screen.getByLabelText('Invoice date'), '2026-09-09'); await user.type(screen.getByLabelText('Due date'), '2026-10-09'); const totals = screen.getAllByText((_, element) => element?.tagName === 'P' && element.textContent?.includes('Net 100,00') && element.textContent.includes('VAT 21,00') && element.textContent.includes('Total 121,00') || false); expect(totals).toHaveLength(1); await user.click(screen.getByRole('button', { name: 'Create draft' })); expect(mocks.createInvoice).toHaveBeenCalledWith(expect.objectContaining({ lines: [expect.objectContaining({ unitPriceMinor: 10000, taxRate: 0.21 })] })); }); });
