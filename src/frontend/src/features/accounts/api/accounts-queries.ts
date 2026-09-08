import { queryOptions, useQuery } from '@tanstack/react-query';
import { getBankAccounts, getPaymentCards } from './accounts-api';

export const accountKeys = {
  all: ['financial-overview'] as const,
  bankAccounts: () => [...accountKeys.all, 'bank-accounts'] as const,
  paymentCards: () => [...accountKeys.all, 'payment-cards'] as const,
};

export const bankAccountsOptions = queryOptions({
  queryKey: accountKeys.bankAccounts(),
  queryFn: ({ signal }) => getBankAccounts(signal),
  staleTime: 30_000,
});

export const paymentCardsOptions = queryOptions({
  queryKey: accountKeys.paymentCards(),
  queryFn: ({ signal }) => getPaymentCards(signal),
  staleTime: 30_000,
});

export function useBankAccounts() {
  return useQuery(bankAccountsOptions);
}

export function usePaymentCards() {
  return useQuery(paymentCardsOptions);
}
