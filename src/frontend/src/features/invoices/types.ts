export type Counterparty = { counterpartyId: string; type: 'customer' | 'supplier'; name: string; status: string; version?: number };
export type InvoiceLineInput = { description: string; quantity: number; unitPriceMinor: number; taxRate: number };
export type Invoice = { invoiceId: string; counterpartyId: string; counterpartyName: string; direction: 'incoming' | 'outgoing'; type: 'service' | 'product'; status: string; netAmountMinor: number; taxAmountMinor: number; totalAmountMinor: number; date: string; dueDate: string; version: number };
export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };
export type CreateInvoiceInput = { projectId: string; counterpartyId: string; direction: 'incoming' | 'outgoing'; type: 'service' | 'product'; lines: InvoiceLineInput[]; date: string; dueDate: string };
export type InvoiceFilters = { status?: string; direction?: 'incoming' | 'outgoing'; type?: 'service' | 'product'; dateFrom?: string; dateTo?: string; totalAmountMinMinor?: number; totalAmountMaxMinor?: number; search?: string; sort: 'date:asc' | 'date:desc' | 'amount:asc' | 'amount:desc'; page: number; pageSize: number };
