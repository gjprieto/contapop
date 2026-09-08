import { apiRequest } from '../../../shared/api/client';
import type { ImportTransactionsInput, ImportTransactionsResult, PagedTransactions, Transaction, TransactionFilters, TransactionInput } from '../types';

function transactionParameters(filters: TransactionFilters) {
  const parameters = new URLSearchParams({ status: filters.status, page: String(filters.page), pageSize: String(filters.pageSize) });
  for (const [key, value] of Object.entries(filters)) if (value && !['status', 'page', 'pageSize'].includes(key)) parameters.set(key, String(value));
  return parameters;
}

export function getTransactions(filters: TransactionFilters, signal?: AbortSignal) {
  return apiRequest<PagedTransactions>(`/experience/v1/transactions?${transactionParameters(filters)}`, { signal });
}

export function createTransaction(input: TransactionInput) {
  return apiRequest<Transaction>('/experience/v1/transactions', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify(input) });
}

export function getTransaction(transactionId: string) {
  return apiRequest<Transaction>(`/experience/v1/transactions/${transactionId}`);
}

export async function updateTransaction(transactionId: string, input: TransactionInput) {
  const transaction = await getTransaction(transactionId);
  return apiRequest<Transaction>(`/experience/v1/transactions/${transactionId}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID(), 'If-Match': `"${transaction.version}"` }, body: JSON.stringify(input) });
}

export async function archiveTransaction(transactionId: string) {
  const transaction = await getTransaction(transactionId);
  return apiRequest<Transaction>(`/experience/v1/transactions/${transactionId}/archive`, { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID(), 'If-Match': `"${transaction.version}"` } });
}

export function importTransactions(input: ImportTransactionsInput) {
  const body = new FormData();
  body.set('file', input.file);
  body.set('bankAccountId', input.bankAccountId);
  body.set('columnMapping', JSON.stringify(input.columnMapping));
  return apiRequest<ImportTransactionsResult>('/experience/v1/transactions/import', { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID() }, body });
}
