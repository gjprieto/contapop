import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { startTransition, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { useCurrentUser } from "../../auth/api/auth-queries";
import {
  changeInvoiceStatus,
  createCounterparty,
  createInvoice,
  downloadInvoice,
} from "../api/invoices-api";
import {
  invoiceKeys,
  useCounterparties,
  useInvoices,
} from "../api/invoices-queries";
import type { Invoice, InvoiceFilters } from "../types";

const schema = z.object({
  counterpartyId: z.string().min(1, "Select a counterparty."),
  direction: z.enum(["incoming", "outgoing"]),
  type: z.enum(["service", "product"]),
  description: z.string().trim().min(1, "Enter a line description."),
  quantity: z.coerce.number<number>().int().positive(),
  unitPrice: z.coerce.number<number>().positive(),
  taxRate: z.coerce.number<number>().min(0).max(1),
  date: z.string().min(1),
  dueDate: z.string().min(1),
});
type Values = z.infer<typeof schema>;

const money = (amount: number) =>
  new Intl.NumberFormat("es-ES", { style: "currency", currency: "EUR" }).format(
    amount / 100,
  );

function filtersFromSearch(search: URLSearchParams): InvoiceFilters {
  const direction = search.get("direction");
  const type = search.get("type");
  const sort = search.get("sort");
  const minValue = search.get("totalAmountMinMinor");
  const maxValue = search.get("totalAmountMaxMinor");
  const min = minValue === null ? undefined : Number(minValue);
  const max = maxValue === null ? undefined : Number(maxValue);
  return {
    status: search.get("status") || undefined,
    direction:
      direction === "incoming" || direction === "outgoing"
        ? direction
        : undefined,
    type: type === "service" || type === "product" ? type : undefined,
    dateFrom: search.get("dateFrom") || undefined,
    dateTo: search.get("dateTo") || undefined,
    totalAmountMinMinor:
      min !== undefined && Number.isInteger(min) && min >= 0 ? min : undefined,
    totalAmountMaxMinor:
      max !== undefined && Number.isInteger(max) && max >= 0 ? max : undefined,
    search: search.get("search") || undefined,
    sort:
      sort === "date:asc" || sort === "amount:asc" || sort === "amount:desc"
        ? sort
        : "date:desc",
    page: Math.max(Number(search.get("page")) || 1, 1),
    pageSize: 10,
  };
}

function InvoiceForm({ onClose }: { onClose: () => void }) {
  const counterparties = useCounterparties();
  const user = useCurrentUser();
  const cache = useQueryClient();
  const [counterpartyName, setCounterpartyName] = useState("");
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      counterpartyId: "",
      direction: "outgoing",
      type: "service",
      description: "",
      quantity: 1,
      unitPrice: 0,
      taxRate: 0.21,
      date: "",
      dueDate: "",
    },
  });
  const values = form.watch();
  const net = Math.round(
    (Number(values.quantity) || 0) * (Number(values.unitPrice) || 0) * 100,
  );
  const tax = Math.round(net * (Number(values.taxRate) || 0));
  const refresh = async () => {
    await cache.invalidateQueries({ queryKey: invoiceKeys.all });
    await cache.invalidateQueries({ queryKey: invoiceKeys.counterparties });
  };
  const addCounterparty = useMutation({
    mutationFn: () =>
      createCounterparty({
        type: values.direction === "outgoing" ? "customer" : "supplier",
        name: counterpartyName,
      }),
    onSuccess: async (item) => {
      form.setValue("counterpartyId", item.counterpartyId);
      setCounterpartyName("");
      await refresh();
    },
  });
  const create = useMutation({
    mutationFn: (input: Values) =>
      createInvoice({
        projectId: user.data?.projectId ?? "",
        counterpartyId: input.counterpartyId,
        direction: input.direction,
        type: input.type,
        lines: [
          {
            description: input.description,
            quantity: input.quantity,
            unitPriceMinor: Math.round(input.unitPrice * 100),
            taxRate: input.taxRate,
          },
        ],
        date: input.date,
        dueDate: input.dueDate,
      }),
    onSuccess: async () => {
      form.reset();
      await refresh();
      onClose();
    },
  });
  return (
    <div className="modal-backdrop" role="presentation">
      <section
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="invoice-form-title"
      >
        <div className="modal-head">
          <h2 id="invoice-form-title">Create invoice</h2>
        </div>
        <form
          onSubmit={form.handleSubmit((input) => create.mutate(input))}
          noValidate
        >
          <div className="modal-body form-grid">
            <div className="form-field full">
              <label htmlFor="invoice-counterparty">Counterparty</label>
              <select
                id="invoice-counterparty"
                {...form.register("counterpartyId")}
              >
                <option value="">Select a counterparty</option>
                {counterparties.data?.items.map((item) => (
                  <option key={item.counterpartyId} value={item.counterpartyId}>
                    {item.name}
                  </option>
                ))}
              </select>
              {form.formState.errors.counterpartyId && (
                <p className="err">
                  {form.formState.errors.counterpartyId.message}
                </p>
              )}
            </div>
            <div className="form-field full">
              <label htmlFor="new-counterparty">Or add a counterparty</label>
              <div className="flex gap-8">
                <input
                  id="new-counterparty"
                  value={counterpartyName}
                  onChange={(event) => setCounterpartyName(event.target.value)}
                />
                <button
                  className="btn"
                  type="button"
                  disabled={!counterpartyName || addCounterparty.isPending}
                  onClick={() => addCounterparty.mutate()}
                >
                  Add
                </button>
              </div>
            </div>
            <div className="form-field">
              <label htmlFor="invoice-direction">Direction</label>
              <select id="invoice-direction" {...form.register("direction")}>
                <option value="outgoing">Outgoing</option>
                <option value="incoming">Incoming</option>
              </select>
            </div>
            <div className="form-field">
              <label htmlFor="invoice-type">Type</label>
              <select id="invoice-type" {...form.register("type")}>
                <option value="service">Service</option>
                <option value="product">Product</option>
              </select>
            </div>
            <div className="form-field full">
              <label htmlFor="invoice-description">Line description</label>
              <input
                id="invoice-description"
                {...form.register("description")}
              />
            </div>
            <div className="form-field">
              <label htmlFor="invoice-quantity">Quantity</label>
              <input
                id="invoice-quantity"
                type="number"
                min="1"
                {...form.register("quantity")}
              />
            </div>
            <div className="form-field">
              <label htmlFor="invoice-unit-price">Unit price (EUR)</label>
              <input
                id="invoice-unit-price"
                type="number"
                min="0.01"
                step="0.01"
                {...form.register("unitPrice")}
              />
            </div>
            <div className="form-field">
              <label htmlFor="invoice-tax-rate">VAT rate</label>
              <input
                id="invoice-tax-rate"
                type="number"
                min="0"
                max="1"
                step="0.01"
                {...form.register("taxRate")}
              />
            </div>
            <div className="form-field">
              <label htmlFor="invoice-date">Invoice date</label>
              <input id="invoice-date" type="date" {...form.register("date")} />
            </div>
            <div className="form-field">
              <label htmlFor="invoice-due-date">Due date</label>
              <input
                id="invoice-due-date"
                type="date"
                {...form.register("dueDate")}
              />
            </div>
            <p className="full">
              Net {money(net)} · VAT {money(tax)} · Total {money(net + tax)}
            </p>
            {create.isError && (
              <p className="form-error full" role="alert">
                We could not create this invoice.
              </p>
            )}
          </div>
          <div className="modal-foot">
            <button className="btn" type="button" onClick={onClose}>
              Cancel
            </button>
            <button
              className="btn btn-primary"
              type="submit"
              disabled={create.isPending}
            >
              {create.isPending ? "Creating..." : "Create draft"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

export function InvoicesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = filtersFromSearch(searchParams);
  const invoices = useInvoices(filters);
  const cache = useQueryClient();
  const [open, setOpen] = useState(false);
  const [invoiceToArchive, setInvoiceToArchive] = useState<Invoice | null>(
    null,
  );
  const lifecycle = useMutation({
    mutationFn: ({
      invoice,
      action,
    }: {
      invoice: Invoice;
      action: "issue" | "void" | "archive";
    }) => changeInvoiceStatus(invoice, action),
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: invoiceKeys.all });
      setInvoiceToArchive(null);
    },
  });
  const updateFilter = (values: Record<string, string | undefined>) =>
    startTransition(() =>
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        for (const [key, value] of Object.entries(values))
          if (value) next.set(key, value);
          else next.delete(key);
        if (!("page" in values)) next.set("page", "1");
        return next;
      }),
    );
  const clearFilters = () => startTransition(() => setSearchParams({}));
  const totalPages = Math.max(
    1,
    Math.ceil((invoices.data?.totalCount ?? 0) / filters.pageSize),
  );
  return (
    <main className="page">
      <div className="page-head">
        <div>
          <p className="eyebrow">Billing</p>
          <h1>Invoices</h1>
          <p className="page-intro">
            Create itemized invoices, issue drafts, and track payment status.
          </p>
        </div>
        <button
          className="btn btn-primary"
          type="button"
          onClick={() => setOpen(true)}
        >
          Create invoice
        </button>
      </div>
      <section className="card">
        <div className="card-body">
          <div className="toolbar">
            <label className="search-box">
              <span className="visually-hidden">Search invoices</span>
              <input
                type="search"
                value={filters.search ?? ""}
                onChange={(event) =>
                  updateFilter({ search: event.target.value || undefined })
                }
                placeholder="Search invoices..."
              />
            </label>
            <select
              aria-label="Invoice status"
              value={filters.status ?? ""}
              onChange={(event) =>
                updateFilter({ status: event.target.value || undefined })
              }
            >
              <option value="">All non-archived</option>
              <option value="draft">Draft</option>
              <option value="issued">Issued</option>
              <option value="paid">Paid</option>
              <option value="overdue">Overdue</option>
              <option value="void">Void</option>
              <option value="archived">Archived</option>
            </select>
            <select
              aria-label="Invoice direction"
              value={filters.direction ?? ""}
              onChange={(event) =>
                updateFilter({ direction: event.target.value || undefined })
              }
            >
              <option value="">All directions</option>
              <option value="incoming">Incoming</option>
              <option value="outgoing">Outgoing</option>
            </select>
            <select
              aria-label="Invoice type"
              value={filters.type ?? ""}
              onChange={(event) =>
                updateFilter({ type: event.target.value || undefined })
              }
            >
              <option value="">All types</option>
              <option value="service">Service</option>
              <option value="product">Product</option>
            </select>
            <input
              aria-label="From invoice date"
              type="date"
              value={filters.dateFrom ?? ""}
              onChange={(event) =>
                updateFilter({ dateFrom: event.target.value || undefined })
              }
            />
            <input
              aria-label="To invoice date"
              type="date"
              value={filters.dateTo ?? ""}
              onChange={(event) =>
                updateFilter({ dateTo: event.target.value || undefined })
              }
            />
            <input
              aria-label="Minimum total amount (EUR)"
              type="number"
              min="0"
              step="0.01"
              value={
                filters.totalAmountMinMinor === undefined
                  ? ""
                  : String(filters.totalAmountMinMinor / 100)
              }
              onChange={(event) =>
                updateFilter({
                  totalAmountMinMinor: event.target.value
                    ? String(Math.round(Number(event.target.value) * 100))
                    : undefined,
                })
              }
            />
            <input
              aria-label="Maximum total amount (EUR)"
              type="number"
              min="0"
              step="0.01"
              value={
                filters.totalAmountMaxMinor === undefined
                  ? ""
                  : String(filters.totalAmountMaxMinor / 100)
              }
              onChange={(event) =>
                updateFilter({
                  totalAmountMaxMinor: event.target.value
                    ? String(Math.round(Number(event.target.value) * 100))
                    : undefined,
                })
              }
            />
            <select
              aria-label="Sort invoices"
              value={filters.sort}
              onChange={(event) => updateFilter({ sort: event.target.value })}
            >
              <option value="date:desc">Newest first</option>
              <option value="date:asc">Oldest first</option>
              <option value="amount:desc">Highest amount</option>
              <option value="amount:asc">Lowest amount</option>
            </select>
            <button className="btn btn-sm" type="button" onClick={clearFilters}>
              Clear filters
            </button>
          </div>
          <p className="result-count">
            {invoices.data?.totalCount ?? 0} invoices
          </p>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Counterparty</th>
                <th>Direction</th>
                <th>Type</th>
                <th>Status</th>
                <th className="num">Total</th>
                <th>Due</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {invoices.data?.items.map((invoice) => (
                <tr key={invoice.invoiceId}>
                  <td className="cell-title">{invoice.counterpartyName}</td>
                  <td>{invoice.direction}</td>
                  <td>{invoice.type}</td>
                  <td>
                    <span
                      className={`badge badge-${invoice.status === "paid" ? "green" : invoice.status === "overdue" ? "red" : "amber"}`}
                    >
                      {invoice.status}
                    </span>
                  </td>
                  <td className="num">{money(invoice.totalAmountMinor)}</td>
                  <td>{invoice.dueDate}</td>
                  <td>
                    <div className="row-actions visible-actions">
                      {invoice.status === "draft" && (
                        <>
                          <button
                            className="btn btn-sm"
                            type="button"
                            onClick={() =>
                              lifecycle.mutate({ invoice, action: "issue" })
                            }
                          >
                            Issue
                          </button>
                          <button
                            className="btn btn-sm"
                            type="button"
                            onClick={() =>
                              lifecycle.mutate({ invoice, action: "void" })
                            }
                          >
                            Void
                          </button>
                        </>
                      )}
                      {invoice.canArchive && (
                        <button
                          className="btn btn-sm"
                          type="button"
                          onClick={() => setInvoiceToArchive(invoice)}
                        >
                          Archive
                        </button>
                      )}
                      <Link
                        className="icon-btn"
                        to={`/invoices/${invoice.invoiceId}${searchParams.toString() ? `?${searchParams}` : ""}`}
                        aria-label={`View invoice details for ${invoice.counterpartyName}`}
                        title="View invoice details"
                      >
                        <svg className="icon" viewBox="0 0 24 24" aria-hidden="true">
                          <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6ZM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
                        </svg>
                      </Link>
                      <button
                        className="icon-btn"
                        type="button"
                        aria-label="Open generated invoice PDF"
                        title="Open generated invoice PDF"
                        onClick={async () => {
                          const blob = await downloadInvoice(invoice.invoiceId);
                          const url = URL.createObjectURL(blob);
                          window.open(url, "_blank", "noopener");
                        }}
                      >
                        <svg
                          className="icon"
                          viewBox="0 0 24 24"
                          aria-hidden="true"
                        >
                          <path d="M6 3h9l5 5v13H6zM14 3v5h5M9 13h6M9 17h6M9 9h2" />
                        </svg>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {invoices.isPending && (
          <p className="card-body" role="status">
            Loading invoices...
          </p>
        )}
        {invoices.isError && (
          <p className="card-body" role="alert">
            We could not load invoices.
          </p>
        )}
        {!invoices.isPending && invoices.data?.items.length === 0 && (
          <div className="empty-state">
            <p className="msg">No invoices found</p>
            <p className="sub">Adjust your filters or create an invoice.</p>
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
      {open && <InvoiceForm onClose={() => setOpen(false)} />}
      {invoiceToArchive && (
        <div className="modal-backdrop" role="presentation">
          <section
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="archive-invoice-title"
            aria-describedby="archive-invoice-description"
          >
            <div className="modal-head">
              <h2 id="archive-invoice-title">Archive invoice?</h2>
            </div>
            <div className="modal-body">
              <p id="archive-invoice-description">
                This invoice will be removed from the default list. Its data
                will be retained.
              </p>
            </div>
            <div className="modal-foot">
              <button
                className="btn"
                type="button"
                disabled={lifecycle.isPending}
                onClick={() => setInvoiceToArchive(null)}
              >
                Cancel
              </button>
              <button
                className="btn btn-primary"
                type="button"
                disabled={lifecycle.isPending}
                onClick={() =>
                  lifecycle.mutate({
                    invoice: invoiceToArchive,
                    action: "archive",
                  })
                }
              >
                {lifecycle.isPending ? "Archiving..." : "Archive invoice"}
              </button>
            </div>
          </section>
        </div>
      )}
    </main>
  );
}
