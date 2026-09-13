import { apiRequest } from '../../shared/api/client';
import type { FinancialRecord, FinancialRecordInput, FinancialRecordPage, ImportResult, TransactionOption } from './types';

type Resource = 'expenses' | 'revenues';
const key = () => crypto.randomUUID();
type TransportRecord = Omit<FinancialRecord, 'id'> & { id?: string; expenseId?: string; revenueId?: string };
export async function getRecords(resource: Resource, signal?: AbortSignal): Promise<FinancialRecordPage> {
  const page = await apiRequest<Omit<FinancialRecordPage, 'items'> & { items: TransportRecord[] }>(`/experience/v1/${resource}?includeDrafts=true&page=1&pageSize=100`, { signal });
  return { ...page, items: page.items.map(item => ({ ...item, id: item.id ?? item.expenseId ?? item.revenueId ?? '' })) };
}
export const getTransactions = (resource: Resource, signal?: AbortSignal) => apiRequest<{ items: TransactionOption[] }>(`/experience/v1/${resource}/unreconciled-transactions?page=1&pageSize=100`, { signal });
async function recordResponse(path: string, init: RequestInit): Promise<FinancialRecord> { const record = await apiRequest<TransportRecord>(path, init); const id = record.id ?? record.expenseId ?? record.revenueId; if (!id) throw new Error('The service returned a record without an ID.'); return { ...record, id }; }
export const createRecord = (resource: Resource, input: FinancialRecordInput) => recordResponse(`/experience/v1/${resource}`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key() }, body: JSON.stringify(input) });
export const updateRecord = (resource: Resource, record: FinancialRecord, input: Partial<FinancialRecordInput>) => recordResponse(`/experience/v1/${resource}/${record.id}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key(), 'If-Match': `"${record.version}"` }, body: JSON.stringify(input) });
export const deleteRecord = (resource: Resource, record: FinancialRecord) => apiRequest<void>(`/experience/v1/${resource}/${record.id}`, { method: 'DELETE', headers: { 'Idempotency-Key': key(), 'If-Match': `"${record.version}"` } });
export const reconcileRecord = (resource: Resource, record: FinancialRecord, transactionId: string) => apiRequest(`/experience/v1/${resource}/${record.id}/reconcile`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key() }, body: JSON.stringify({ transactionId, recordVersion: record.version }) });
export const confirmRecord = (resource: Resource, record: FinancialRecord, input: Partial<FinancialRecordInput>) => recordResponse(`/experience/v1/${resource}/${record.id}/confirm`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key(), 'If-Match': `"${record.version}"` }, body: JSON.stringify(input) });
export async function importRecords(resource: Resource, action: 'import-file' | 'import-document', file: File, projectId: string, mapping?: object) {
  const form = new FormData(); form.set('file', file); form.set('projectId', projectId); if (mapping) form.set('columnMapping', JSON.stringify(mapping));
  const response = await apiRequest<ImportResult | TransportRecord>(`/experience/v1/${resource}/${action}`, { method: 'POST', headers: { 'Idempotency-Key': key() }, body: form });
  if ('importedCount' in response) return response;
  const id = response.id ?? response.expenseId ?? response.revenueId;
  if (!id) throw new Error('The service returned an import without a record ID.');
  return { ...response, id } as FinancialRecord;
}
