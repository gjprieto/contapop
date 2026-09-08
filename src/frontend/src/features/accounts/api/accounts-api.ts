import { apiRequest, apiRequestVoid } from '../../../shared/api/client';
import type { BankAccount, CreateBankAccountInput, CreatePaymentCardInput, PagedResult, PaymentCard } from '../types';

export function getBankAccounts(signal?: AbortSignal) {
  return apiRequest<PagedResult<BankAccount>>('/experience/v1/financial-overview/bank-accounts', { signal });
}

export function getPaymentCards(signal?: AbortSignal) {
  return apiRequest<PagedResult<PaymentCard>>('/experience/v1/financial-overview/payment-cards', { signal });
}

export function createBankAccount(input: CreateBankAccountInput) {
  return apiRequest<BankAccount>('/experience/v1/financial-overview/bank-accounts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify(input),
  });
}

export function archiveBankAccount(bankAccountId: string, version: number) {
  return apiRequest<BankAccount>(`/experience/v1/financial-overview/bank-accounts/${bankAccountId}/archive`, {
    method: 'POST',
    headers: { 'Idempotency-Key': crypto.randomUUID(), 'If-Match': `"${version}"` },
  });
}

export function createPaymentCard(input: CreatePaymentCardInput) {
  return apiRequest<PaymentCard>('/experience/v1/financial-overview/payment-cards', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify(input),
  });
}

export function removePaymentCard(cardId: string, version: number) {
  return apiRequestVoid(`/experience/v1/financial-overview/payment-cards/${cardId}`, {
    method: 'DELETE',
    headers: { 'Idempotency-Key': crypto.randomUUID(), 'If-Match': `"${version}"` },
  });
}
