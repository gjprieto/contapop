import { apiRequest, apiRequestVoid } from "../../../shared/api/client";
import type {
  Counterparty,
  CreateInvoiceInput,
  UpdateDraftInvoiceInput,
  Invoice,
  InvoiceDetail,
  InvoiceFilters,
  Paged,
} from "../types";

function invoiceParameters(filters: InvoiceFilters) {
  const parameters = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    sort: filters.sort,
  });
  for (const [key, value] of Object.entries(filters))
    if (
      value !== undefined &&
      value !== "" &&
      !["page", "pageSize", "sort"].includes(key)
    )
      parameters.set(key, String(value));
  return parameters;
}

export const getInvoices = (filters: InvoiceFilters, signal?: AbortSignal) =>
  apiRequest<Paged<Invoice>>(
    `/experience/v1/invoices?${invoiceParameters(filters)}`,
    { signal },
  );
export const getInvoice = (invoiceId: string, signal?: AbortSignal) =>
  apiRequest<InvoiceDetail>(`/experience/v1/invoices/${invoiceId}`, { signal });
export const getCounterparties = (signal?: AbortSignal) =>
  apiRequest<Paged<Counterparty>>(
    "/experience/v1/counterparties?page=1&pageSize=100",
    { signal },
  );
export const createCounterparty = (input: { type: string; name: string }) =>
  apiRequest<Counterparty>("/experience/v1/counterparties", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(input),
  });
export const createInvoice = (input: CreateInvoiceInput) =>
  apiRequest<Invoice>("/experience/v1/invoices", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(input),
  });
export const updateDraftInvoice = (
  invoice: InvoiceDetail,
  input: UpdateDraftInvoiceInput,
) =>
  apiRequest<InvoiceDetail>(`/experience/v1/invoices/${invoice.invoiceId}`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
      "If-Match": `"${invoice.version}"`,
    },
    body: JSON.stringify(input),
  });
export const changeInvoiceStatus = (
  invoice: Invoice,
  action: "issue" | "void" | "archive",
) =>
  apiRequest<Invoice>(
    `/experience/v1/invoices/${invoice.invoiceId}/${action}`,
    {
      method: "POST",
      headers: {
        "Idempotency-Key": crypto.randomUUID(),
        "If-Match": `"${invoice.version}"`,
      },
    },
  );
export const deleteDraftInvoice = (invoice: Invoice) =>
  apiRequestVoid(`/experience/v1/invoices/${invoice.invoiceId}`, {
    method: "DELETE",
    headers: {
      "Idempotency-Key": crypto.randomUUID(),
      "If-Match": `"${invoice.version}"`,
    },
  });
export async function uploadInvoiceAttachment(invoiceId: string, file: File, attachmentType?: "invoice" | "other") {
  const form = new FormData();
  form.append("file", file);
  if (attachmentType) form.append("attachmentType", attachmentType);
  const response = await fetch(`/experience/v1/invoices/${invoiceId}/attachments`, {
    method: "POST",
    headers: { "Idempotency-Key": crypto.randomUUID() },
    body: form,
    credentials: "include",
  });
  if (!response.ok) throw new Error("Could not upload attachment.");
  return response.json();
}
export async function replaceInvoiceAttachment(invoiceId: string, attachmentId: string, version: number, file: File) {
  const form = new FormData();
  form.append("file", file);
  const response = await fetch(`/experience/v1/invoices/${invoiceId}/attachments/${attachmentId}`, { method: "PUT", headers: { "Idempotency-Key": crypto.randomUUID(), "If-Match": `"${version}"` }, body: form, credentials: "include" });
  if (!response.ok) throw new Error("Could not replace attachment.");
  return response.json();
}
export async function removeInvoiceAttachment(invoiceId: string, attachmentId: string) {
  const response = await fetch(`/experience/v1/invoices/${invoiceId}/attachments/${attachmentId}`, { method: "DELETE", headers: { "Idempotency-Key": crypto.randomUUID() }, credentials: "include" });
  if (!response.ok) throw new Error("Could not remove attachment.");
}
export async function downloadInvoiceAttachment(invoiceId: string, attachmentId: string) {
  const response = await fetch(`/experience/v1/invoices/${invoiceId}/attachments/${attachmentId}`, { credentials: "include" });
  if (!response.ok) throw new Error("Could not download attachment.");
  return response.blob();
}
