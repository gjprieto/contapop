import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { startTransition, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { useSearchParams } from "react-router-dom";
import { z } from "zod";
import { useBankAccounts } from "../../accounts/api/accounts-queries";
import { ApiError } from "../../../shared/api/client";
import {
  archiveTransaction,
  createTransaction,
  importTransactions,
  updateTransaction,
} from "../api/transactions-api";
import { transactionKeys, useTransactions } from "../api/transactions-queries";
import type {
  Transaction,
  TransactionFilters,
  TransactionInput,
  TransactionStatus,
} from "../types";

const transactionSchema = z.object({
  bankAccountId: z.string().min(1, "Select a bank account."),
  amount: z
    .string()
    .refine((value) => Number(value) > 0, "Enter an amount greater than zero."),
  date: z.string().min(1, "Select a date."),
  type: z.enum(["income", "expense"]),
  description: z.string().trim().max(500),
});
type TransactionValues = z.infer<typeof transactionSchema>;
type ImportStep = "file" | "mapping" | "result";

function filtersFromSearch(search: URLSearchParams): TransactionFilters {
  const type = search.get("type");
  const status = search.get("status");
  return {
    bankAccountId: search.get("bankAccountId") || undefined,
    type: type === "income" || type === "expense" ? type : undefined,
    status: status === "archived" ? "archived" : "active",
    dateFrom: search.get("dateFrom") || undefined,
    dateTo: search.get("dateTo") || undefined,
    search: search.get("search") || undefined,
    sort: search.get("sort") || "date:desc",
    page: Number(search.get("page") ?? 1),
    pageSize: 10,
  };
}

function formatMoney(amountMinor: number) {
  return new Intl.NumberFormat("es-ES", {
    style: "currency",
    currency: "EUR",
  }).format(amountMinor / 100);
}

function TransactionForm({
  transaction,
  onClose,
}: {
  transaction?: Transaction;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const accounts = useBankAccounts();
  const form = useForm<TransactionValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: transaction
      ? {
          bankAccountId: transaction.bankAccountId,
          amount: String(Math.abs(transaction.amountMinor) / 100),
          date: transaction.date,
          type: transaction.type,
          description: transaction.description ?? "",
        }
      : {
          bankAccountId: "",
          amount: "",
          date: "",
          type: "expense",
          description: "",
        },
  });
  const save = useMutation({
    mutationFn: (values: TransactionValues) => {
      const input: TransactionInput = {
        bankAccountId: values.bankAccountId,
        amountMinor: Math.round(Number(values.amount) * 100),
        date: values.date,
        type: values.type,
        description: values.description || undefined,
      };
      return transaction
        ? updateTransaction(transaction.transactionId, input)
        : createTransaction(input);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: transactionKeys.all });
      onClose();
    },
  });
  return (
    <div className="modal-backdrop" role="presentation">
      <section
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="transaction-form-title"
      >
        <div className="modal-head">
          <h2 id="transaction-form-title">
            {transaction ? "Edit transaction" : "Record transaction"}
          </h2>
        </div>
        <form
          onSubmit={form.handleSubmit((values) => save.mutate(values))}
          noValidate
        >
          <div className="modal-body form-grid">
            <div className="form-field full">
              <label htmlFor="transaction-account">Bank account</label>
              <select
                id="transaction-account"
                {...form.register("bankAccountId")}
              >
                <option value="">Select an account</option>
                {accounts.data?.items.map((account) => (
                  <option
                    key={account.bankAccountId}
                    value={account.bankAccountId}
                  >
                    {account.bankName} · {account.accountNumber}
                  </option>
                ))}
              </select>
              {form.formState.errors.bankAccountId && (
                <p className="err">
                  {form.formState.errors.bankAccountId.message}
                </p>
              )}
            </div>
            <div className="form-field">
              <label htmlFor="transaction-amount">Amount (EUR)</label>
              <input
                id="transaction-amount"
                type="number"
                min="0.01"
                step="0.01"
                {...form.register("amount")}
              />
              {form.formState.errors.amount && (
                <p className="err">{form.formState.errors.amount.message}</p>
              )}
            </div>
            <div className="form-field">
              <label htmlFor="transaction-date">Date</label>
              <input
                id="transaction-date"
                type="date"
                {...form.register("date")}
              />
              {form.formState.errors.date && (
                <p className="err">{form.formState.errors.date.message}</p>
              )}
            </div>
            <div className="form-field">
              <label htmlFor="transaction-type">Type</label>
              <select id="transaction-type" {...form.register("type")}>
                <option value="expense">Expense</option>
                <option value="income">Income</option>
              </select>
            </div>
            <div className="form-field">
              <label htmlFor="transaction-description">Description</label>
              <input
                id="transaction-description"
                {...form.register("description")}
              />
            </div>
            {save.isError && (
              <p className="form-error full" role="alert">
                We could not save this transaction.
              </p>
            )}
          </div>
          <div className="modal-foot">
            <button type="button" className="btn" onClick={onClose}>
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={save.isPending}
            >
              {save.isPending ? "Saving..." : "Save transaction"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

function ImportWizard({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const accounts = useBankAccounts();
  const [step, setStep] = useState<ImportStep>("file");
  const [file, setFile] = useState<File>();
  const [bankAccountId, setBankAccountId] = useState("");
  const [columns, setColumns] = useState({
    dateColumn: "",
    amountColumn: "",
    typeColumn: "",
    descriptionColumn: "",
  });
  const upload = useMutation({
    mutationFn: () =>
      file
        ? importTransactions({
            file,
            bankAccountId,
            columnMapping: {
              dateColumn: columns.dateColumn,
              amountColumn: columns.amountColumn,
              typeColumn: columns.typeColumn || undefined,
              descriptionColumn: columns.descriptionColumn || undefined,
            },
          })
        : Promise.reject(new Error("Select a file.")),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: transactionKeys.all });
      setStep("result");
    },
  });
  const stepNumber = step === "file" ? 1 : step === "mapping" ? 2 : 3;
  return (
    <div className="modal-backdrop" role="presentation">
      <section
        className="modal modal-lg"
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-title"
      >
        <div className="modal-head">
          <h2 id="import-title">Import transactions</h2>
        </div>
        <div className="modal-body">
          <div
            className="step-indicator"
            aria-label={`Step ${stepNumber} of 3`}
          >
            <span className={`step ${stepNumber >= 1 ? "active" : ""}`}>
              <span className="num">1</span>File
            </span>
            <span className="sep" />
            <span className={`step ${stepNumber >= 2 ? "active" : ""}`}>
              <span className="num">2</span>Map columns
            </span>
            <span className="sep" />
            <span className={`step ${stepNumber >= 3 ? "active" : ""}`}>
              <span className="num">3</span>Complete
            </span>
          </div>
          {step === "file" && (
            <>
              <label className="dropzone" htmlFor="transaction-file">
                <strong>Choose a CSV or Excel file</strong>
                <br />
                <span className="muted">
                  UTF-8 CSV or .xlsx files are supported.
                </span>
              </label>
              <input
                id="transaction-file"
                aria-label="Choose a CSV or Excel file"
                className="visually-hidden"
                type="file"
                accept=".csv,.xlsx,text/csv"
                onChange={(event) => setFile(event.target.files?.[0])}
              />
              <p className="mt-8">
                {file ? `Selected: ${file.name}` : "No file selected."}
              </p>
              <div className="form-field mt-16">
                <label htmlFor="import-account">Bank account</label>
                <select
                  id="import-account"
                  value={bankAccountId}
                  onChange={(event) => setBankAccountId(event.target.value)}
                >
                  <option value="">Select an account</option>
                  {accounts.data?.items.map((account) => (
                    <option
                      key={account.bankAccountId}
                      value={account.bankAccountId}
                    >
                      {account.bankName} · {account.accountNumber}
                    </option>
                  ))}
                </select>
              </div>
            </>
          )}
          {step === "mapping" && (
            <div className="form-grid">
              <div className="form-field">
                <label htmlFor="date-column">Date column</label>
                <input
                  id="date-column"
                  value={columns.dateColumn}
                  onChange={(event) =>
                    setColumns({ ...columns, dateColumn: event.target.value })
                  }
                  placeholder="Date"
                />
              </div>
              <div className="form-field">
                <label htmlFor="amount-column">Amount column</label>
                <input
                  id="amount-column"
                  value={columns.amountColumn}
                  onChange={(event) =>
                    setColumns({ ...columns, amountColumn: event.target.value })
                  }
                  placeholder="Amount"
                />
              </div>
              <div className="form-field">
                <label htmlFor="type-column">Type column (optional)</label>
                <input
                  id="type-column"
                  value={columns.typeColumn}
                  onChange={(event) =>
                    setColumns({ ...columns, typeColumn: event.target.value })
                  }
                  placeholder="Type"
                />
              </div>
              <div className="form-field">
                <label htmlFor="description-column">
                  Description column (optional)
                </label>
                <input
                  id="description-column"
                  value={columns.descriptionColumn}
                  onChange={(event) =>
                    setColumns({
                      ...columns,
                      descriptionColumn: event.target.value,
                    })
                  }
                  placeholder="Description"
                />
              </div>
            </div>
          )}
          {step === "result" && (
            <div role="status" className="empty-state">
              <p className="msg">Import complete</p>
              <p className="sub">
                Your imported transactions are now available in the list.
              </p>
            </div>
          )}
          {upload.isError && (
            <p className="form-error" role="alert">
              {upload.error instanceof ApiError
                ? upload.error.message
                : "We could not import this file. Check the file and column mapping."}
            </p>
          )}
        </div>
        <div className="modal-foot">
          {step !== "result" && (
            <button
              type="button"
              className="btn"
              onClick={() => {
                if (step === "mapping") setStep("file");
                else onClose();
              }}
            >
              Cancel
            </button>
          )}
          {step === "file" && (
            <button
              type="button"
              className="btn btn-primary"
              disabled={!file || !bankAccountId}
              onClick={() => setStep("mapping")}
            >
              Continue
            </button>
          )}
          {step === "mapping" && (
            <button
              type="button"
              className="btn btn-primary"
              disabled={
                !columns.dateColumn || !columns.amountColumn || upload.isPending
              }
              onClick={() => upload.mutate()}
            >
              {upload.isPending ? "Importing..." : "Import transactions"}
            </button>
          )}
          {step === "result" && (
            <button type="button" className="btn btn-primary" onClick={onClose}>
              Done
            </button>
          )}
        </div>
      </section>
    </div>
  );
}

export function TransactionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = filtersFromSearch(searchParams);
  const [dateFrom, setDateFrom] = useState(filters.dateFrom ?? "");
  const [dateTo, setDateTo] = useState(filters.dateTo ?? "");
  const transactions = useTransactions(filters);
  const queryClient = useQueryClient();
  const [formTransaction, setFormTransaction] = useState<Transaction>();
  const [formOpen, setFormOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const archive = useMutation({
    mutationFn: archiveTransaction,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: transactionKeys.all });
    },
  });
  const updateFilter = (values: Record<string, string | undefined>) =>
    startTransition(() =>
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        for (const [key, value] of Object.entries(values)) {
          if (value) next.set(key, value);
          else next.delete(key);
        }
        if (!("page" in values)) next.set("page", "1");
        return next;
      }),
    );
  useEffect(() => setDateFrom(filters.dateFrom ?? ""), [filters.dateFrom]);
  useEffect(() => setDateTo(filters.dateTo ?? ""), [filters.dateTo]);
  const totalPages = Math.max(
    1,
    Math.ceil((transactions.data?.totalCount ?? 0) / filters.pageSize),
  );
  return (
    <main className="page">
      <div className="page-head">
        <div>
          <p className="eyebrow">Money</p>
          <h1>Transactions</h1>
          <p className="page-intro">
            Record, import, and review every bank-side movement.
          </p>
        </div>
        <div className="page-head-actions">
          <button
            type="button"
            className="btn"
            onClick={() => setImportOpen(true)}
          >
            Import CSV
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              setFormTransaction(undefined);
              setFormOpen(true);
            }}
          >
            Record transaction
          </button>
        </div>
      </div>
      <section className="card">
        <div className="card-body">
          <div className="toolbar">
            <label className="search-box">
              <span className="visually-hidden">Search transactions</span>
              <input
                type="search"
                value={filters.search ?? ""}
                onChange={(event) =>
                  updateFilter({ search: event.target.value || undefined })
                }
                placeholder="Search transactions..."
              />
            </label>
            <select
              aria-label="Transaction type"
              value={filters.type ?? ""}
              onChange={(event) =>
                updateFilter({ type: event.target.value || undefined })
              }
            >
              <option value="">All types</option>
              <option value="income">Income</option>
              <option value="expense">Expenses</option>
            </select>
            <select
              aria-label="Transaction status"
              value={filters.status}
              onChange={(event) =>
                updateFilter({
                  status: event.target.value as TransactionStatus,
                })
              }
            >
              <option value="active">Active</option>
              <option value="archived">Archived</option>
            </select>
            <input
              aria-label="From date"
              type="date"
              value={dateFrom}
              onChange={(event) => setDateFrom(event.target.value)}
              onBlur={() =>
                updateFilter({ dateFrom: dateFrom || undefined })
              }
            />
            <input
              aria-label="To date"
              type="date"
              value={dateTo}
              onChange={(event) => setDateTo(event.target.value)}
              onBlur={() => updateFilter({ dateTo: dateTo || undefined })}
            />
            <select
              aria-label="Sort transactions"
              value={filters.sort}
              onChange={(event) => updateFilter({ sort: event.target.value })}
            >
              <option value="date-desc">Newest first</option>
              <option value="date-asc">Oldest first</option>
              <option value="amount-desc">Highest amount</option>
              <option value="amount-asc">Lowest amount</option>
            </select>
          </div>
          <p className="result-count">
            {transactions.data?.totalCount ?? 0} transactions
          </p>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th>Type</th>
                <th className="num">Amount</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {transactions.data?.items.map((transaction) => (
                <tr key={transaction.transactionId}>
                  <td>{transaction.date}</td>
                  <td>
                    <p className="cell-title">
                      {transaction.description || "No description"}
                    </p>
                  </td>
                  <td>
                    <span
                      className={`badge ${transaction.type === "income" ? "badge-green" : "badge-red"}`}
                    >
                      {transaction.type}
                    </span>
                  </td>
                  <td className="num">
                    {transaction.type === "expense" ? "-" : "+"}
                    {formatMoney(transaction.amountMinor)}
                  </td>
                  <td>
                    <div className="row-actions visible-actions">
                      <button
                        type="button"
                        className="btn btn-sm"
                        disabled={transaction.status === "archived"}
                        onClick={() => {
                          setFormTransaction(transaction);
                          setFormOpen(true);
                        }}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        className="btn btn-sm"
                        disabled={
                          transaction.status === "archived" || archive.isPending
                        }
                        onClick={() =>
                          archive.mutate(transaction.transactionId)
                        }
                      >
                        Archive
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {transactions.isPending && (
          <p className="card-body" role="status">
            Loading transactions...
          </p>
        )}
        {transactions.isError && (
          <p className="card-body" role="alert">
            We could not load transactions.
          </p>
        )}
        {!transactions.isPending && transactions.data?.items.length === 0 && (
          <div className="empty-state">
            <p className="msg">No transactions found</p>
            <p className="sub">
              Adjust your filters or record a new transaction.
            </p>
          </div>
        )}
        <div className="pagination">
          <span>
            Page {filters.page} of {totalPages}
          </span>
          <div className="pg-btns">
            <button
              type="button"
              aria-label="Previous page"
              disabled={filters.page <= 1}
              onClick={() => updateFilter({ page: String(filters.page - 1) })}
            >
              ‹
            </button>
            <button
              type="button"
              aria-label="Next page"
              disabled={filters.page >= totalPages}
              onClick={() => updateFilter({ page: String(filters.page + 1) })}
            >
              ›
            </button>
          </div>
        </div>
      </section>
      {formOpen && (
        <TransactionForm
          transaction={formTransaction}
          onClose={() => setFormOpen(false)}
        />
      )}
      {importOpen && <ImportWizard onClose={() => setImportOpen(false)} />}
    </main>
  );
}
