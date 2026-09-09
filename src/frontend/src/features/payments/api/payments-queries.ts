import { useQuery } from '@tanstack/react-query';
import { getPayments, getUnreconciledTransactions } from './payments-api';
export const paymentKeys = { all: ['payments'] as const, transactions: ['payments', 'unreconciled-transactions'] as const };
export const usePayments = () => useQuery({ queryKey: paymentKeys.all, queryFn: ({ signal }) => getPayments(signal) });
export const useUnreconciledTransactions = () => useQuery({ queryKey: paymentKeys.transactions, queryFn: ({ signal }) => getUnreconciledTransactions(signal) });
