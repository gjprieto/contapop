import { useQuery } from '@tanstack/react-query';
import { getCounterparties, getInvoices } from './invoices-api';
import type { InvoiceFilters } from '../types';
export const invoiceKeys = { all: ['invoices'] as const, list: (filters: InvoiceFilters) => [...invoiceKeys.all, 'list', filters] as const, counterparties: ['counterparties'] as const };
export const useInvoices = (filters: InvoiceFilters = { sort: 'date:desc', page: 1, pageSize: 100 }) => useQuery({ queryKey: invoiceKeys.list(filters), queryFn: ({ signal }) => getInvoices(filters, signal) });
export const useCounterparties = () => useQuery({ queryKey: invoiceKeys.counterparties, queryFn: ({ signal }) => getCounterparties(signal) });
