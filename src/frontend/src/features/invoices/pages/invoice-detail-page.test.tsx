import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { InvoiceDetailPage } from "./invoice-detail-page";

const mocks = vi.hoisted(() => ({ getInvoice: vi.fn(), removeInvoiceAttachment: vi.fn() }));
vi.mock("../api/invoices-api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/invoices-api")>()),
  getInvoice: mocks.getInvoice,
  removeInvoiceAttachment: mocks.removeInvoiceAttachment,
}));

describe("InvoiceDetailPage", () => {
  it("shows invoice lines and payment reconciliation, and returns to the filtered list", async () => {
    mocks.getInvoice.mockResolvedValue({
      invoiceId: "invoice-1", projectId: "project-1", counterpartyId: "counterparty-1", counterpartyName: "Acme SL",
      direction: "outgoing", type: "service", status: "paid", canArchive: false,
      netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100, date: "2026-09-09", dueDate: "2026-10-09", version: 2,
      lines: [{ invoiceLineId: "line-1", description: "Consulting", quantity: 1, unitPriceMinor: 10000, taxRate: 0.21, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100 }], attachments: [],
      payments: [{ paymentId: "payment-1", amountMinor: 12100, date: "2026-09-10", paymentMethod: "bank_transfer", reconciledTransactionId: "transaction-1" }],
      createdAt: "2026-09-09T00:00:00Z", updatedAt: "2026-09-10T00:00:00Z",
    });
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={["/invoices/invoice-1?status=paid"]}><Routes><Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>);

    expect(await screen.findByText("Consulting")).toBeVisible();
    expect(screen.getByText("Reconciled")).toBeVisible();
    expect(screen.getByRole("link", { name: "Back to invoices" })).toHaveAttribute("href", "/invoices?status=paid");
});

  it("shows attachment metadata and requires confirmation before removal", async () => {
    mocks.getInvoice.mockResolvedValue({
      invoiceId: "invoice-1", projectId: "project-1", counterpartyId: "counterparty-1", counterpartyName: "Acme SL", direction: "outgoing", type: "service", status: "issued", canArchive: true, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100, date: "2026-09-09", dueDate: "2026-10-09", version: 2,
      lines: [], payments: [], createdAt: "2026-09-09T00:00:00Z", updatedAt: "2026-09-10T00:00:00Z",
      attachments: [{ attachmentId: "attachment-1", invoiceId: "invoice-1", type: "invoice", fileName: "source.pdf", contentType: "application/pdf", sizeBytes: 2048, createdAt: "2026-09-09T00:00:00Z", updatedAt: "2026-09-09T00:00:00Z", version: 1 }],
    });
    mocks.removeInvoiceAttachment.mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={["/invoices/invoice-1"]}><Routes><Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>);

    expect(await screen.findByText(/source.pdf/)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Remove attachment" }));
    expect(screen.getByRole("dialog", { name: "Remove attachment?" })).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(mocks.removeInvoiceAttachment).not.toHaveBeenCalled();
  });

  it("prompts to classify an upload when no invoice attachment exists", async () => {
    mocks.getInvoice.mockResolvedValue({
      invoiceId: "invoice-1", projectId: "project-1", counterpartyId: "counterparty-1", counterpartyName: "Acme SL", direction: "outgoing", type: "service", status: "draft", canArchive: false, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100, date: "2026-09-09", dueDate: "2026-10-09", version: 1,
      lines: [], payments: [], attachments: [], createdAt: "2026-09-09T00:00:00Z", updatedAt: "2026-09-10T00:00:00Z",
    });
    const user = userEvent.setup();
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={["/invoices/invoice-1"]}><Routes><Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>);

    await user.click(await screen.findByRole("button", { name: "Upload attachment" }));

    expect(screen.getByRole("dialog", { name: "Choose attachment type" })).toBeVisible();
    expect(screen.getByText("Other type of attachment")).toBeVisible();
  });
});
