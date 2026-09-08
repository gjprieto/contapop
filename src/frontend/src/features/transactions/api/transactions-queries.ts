import { queryOptions, useQuery } from '@tanstack/react-query';
import { getTransactions } from './transactions-api';
import type { TransactionFilters } from '../types';

export const transactionKeys = { all: ['transactions'] as const, list: (filters: TransactionFilters) => [...transactionKeys.all, 'list', filters] as const };
export function transactionListOptions(filters: TransactionFilters) { return queryOptions({ queryKey: transactionKeys.list(filters), queryFn: ({ signal }) => getTransactions(filters, signal), staleTime: 30_000 }); }
export function useTransactions(filters: TransactionFilters) { return useQuery(transactionListOptions(filters)); }
