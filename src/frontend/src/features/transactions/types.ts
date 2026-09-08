export type TransactionType = 'income' | 'expense';
export type TransactionStatus = 'active' | 'archived';

export type Transaction = {
  transactionId: string;
  bankAccountId: string;
  amountMinor: number;
  date: string;
  type: TransactionType;
  description?: string;
  status: TransactionStatus;
  version?: number;
};

export type TransactionFilters = {
  bankAccountId?: string;
  type?: TransactionType;
  status: TransactionStatus;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
  sort?: string;
  page: number;
  pageSize: number;
};

export type PagedTransactions = { items: Transaction[]; page: number; pageSize: number; totalCount: number };
export type TransactionInput = { bankAccountId: string; amountMinor: number; date: string; type: TransactionType; description?: string };
export type ImportTransactionsInput = { file: File; bankAccountId: string; columnMapping: { dateColumn: string; amountColumn: string; typeColumn?: string; descriptionColumn?: string } };
export type ImportTransactionsResult = { importedCount: number; transactionIds: string[]; skippedRows: Array<{ rowNumber: number; reason: string }> };
