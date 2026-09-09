import { useQuery } from '@tanstack/react-query';
import { getCounterparties, getInvoices } from './invoices-api';
export const invoiceKeys = { all: ['invoices'] as const, counterparties: ['counterparties'] as const };
export const useInvoices = () => useQuery({ queryKey: invoiceKeys.all, queryFn: ({ signal }) => getInvoices(signal) });
export const useCounterparties = () => useQuery({ queryKey: invoiceKeys.counterparties, queryFn: ({ signal }) => getCounterparties(signal) });
