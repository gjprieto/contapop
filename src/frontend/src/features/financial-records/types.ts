export type FinancialRecord = {
  id: string;
  projectId: string;
  amountMinor: number;
  date: string;
  category: string;
  recurring: boolean;
  recurringInterval?: 'weekly' | 'monthly' | 'yearly';
  importSource: 'manual' | 'csv_excel' | 'pdf_ocr';
  confirmedAt?: string;
  reconciledTransactionId?: string;
  version: number;
};

export type FinancialRecordPage = { items: FinancialRecord[]; page: number; pageSize: number; totalCount: number };
export type TransactionOption = { transactionId: string; amountMinor: number; date: string; description?: string };
export type ImportResult = { importedCount: number; recordIds: string[]; skippedRows: Array<{ rowNumber: number; reason: string }> };
export type FinancialRecordInput = { projectId: string; amountMinor: number; date: string; category: string; recurring: boolean; recurringInterval?: string };
