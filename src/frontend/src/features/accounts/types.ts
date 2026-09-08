export type BankAccount = {
  bankAccountId: string;
  accountNumber: string;
  bankName: string;
  status: 'active' | 'archived';
  balanceMinor: number;
  version: number;
};

export type PaymentCard = {
  cardId: string;
  label: string;
  cardholderName: string;
  expirationDate?: string;
  version: number;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type CreateBankAccountInput = {
  projectId: string;
  accountNumber: string;
  bankName: string;
};

export type CreatePaymentCardInput = {
  projectId: string;
  label: string;
  cardholderName: string;
  expirationDate?: string;
};
