import { useQuery } from '@tanstack/react-query';
import { getRecords, getTransactions } from './api';
export const financialRecordKeys = { all: ['financial-records'] as const, records: (resource: string) => [...financialRecordKeys.all, resource] as const, transactions: (resource: string) => [...financialRecordKeys.records(resource), 'transactions'] as const };
export const useFinancialRecords = (resource: 'expenses' | 'revenues') => useQuery({ queryKey: financialRecordKeys.records(resource), queryFn: ({ signal }) => getRecords(resource, signal) });
export const useFinancialRecordTransactions = (resource: 'expenses' | 'revenues') => useQuery({ queryKey: financialRecordKeys.transactions(resource), queryFn: ({ signal }) => getTransactions(resource, signal) });
