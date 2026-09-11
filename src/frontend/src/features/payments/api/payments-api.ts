import { apiRequest } from '../../../shared/api/client';
import type { Paged } from '../../invoices/types';
export type Payment = { paymentId: string; invoiceId: string; amountMinor: number; date: string; paymentMethod: string; reconciledTransactionId?: string; version: number };
export type Transaction = { transactionId: string; description?: string; amountMinor: number; date: string };
export const getPayments = (signal?: AbortSignal) => apiRequest<Paged<Payment>>('/experience/v1/payments?page=1&pageSize=100', { signal });
export const getUnreconciledTransactions = (signal?: AbortSignal) => apiRequest<Paged<Transaction>>('/experience/v1/payments/unreconciled-transactions?page=1&pageSize=100', { signal });
export const recordPayment = (input: { invoiceId: string; amountMinor: number; date: string; paymentMethod: string }) => apiRequest<Payment>('/experience/v1/payments', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify(input) });
export const reconcilePayment = (payment: Payment, transactionId: string) => apiRequest(`/experience/v1/payments/${payment.paymentId}/reconcile`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify({ transactionId, paymentVersion: payment.version }) });
