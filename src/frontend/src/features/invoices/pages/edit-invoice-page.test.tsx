import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { EditInvoicePage } from "./edit-invoice-page";

const mocks = vi.hoisted(() => ({ getInvoice: vi.fn(), updateDraftInvoice: vi.fn() }));
vi.mock("../api/invoices-api", async importOriginal => ({ ...(await importOriginal<typeof import("../api/invoices-api")>()), getInvoice: mocks.getInvoice, updateDraftInvoice: mocks.updateDraftInvoice }));

function renderPage() {
  mocks.getInvoice.mockResolvedValue({ invoiceId: "invoice-1", projectId: "project-1", counterpartyId: "counterparty-1", counterpartyName: "Acme SL", direction: "outgoing", type: "service", status: "draft", canArchive: false, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100, date: "2026-09-09", dueDate: "2026-10-09", version: 1, lines: [{ invoiceLineId: "line-1", description: "Consulting", quantity: 1, unitPriceMinor: 10000, taxRate: 0.21, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100 }], payments: [], createdAt: "2026-09-09T00:00:00Z", updatedAt: "2026-09-09T00:00:00Z" });
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={["/invoices/invoice-1/edit"]}><Routes><Route path="/invoices/:invoiceId/edit" element={<EditInvoicePage />} /><Route path="/invoices/:invoiceId" element={<p>Invoice detail</p>} /></Routes></MemoryRouter></QueryClientProvider>);
}

describe("EditInvoicePage", () => {
  beforeEach(() => vi.clearAllMocks());
  it("edits a prepopulated draft, adds/removes lines, and submits recalculated line inputs", async () => {
    const user = userEvent.setup(); mocks.updateDraftInvoice.mockResolvedValue({}); renderPage();
    expect(await screen.findByDisplayValue("Consulting")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Add line" }));
    await user.type(screen.getByLabelText("Line description", { selector: "#line-1-description" }), "Support");
    await user.clear(screen.getByLabelText("Unit price (EUR)", { selector: "#line-1-price" }));
    await user.type(screen.getByLabelText("Unit price (EUR)", { selector: "#line-1-price" }), "50");
    expect(screen.getByText(/Total 181,50/)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(mocks.updateDraftInvoice).toHaveBeenCalledWith(expect.objectContaining({ invoiceId: "invoice-1" }), expect.objectContaining({ lines: expect.arrayContaining([expect.objectContaining({ description: "Support", unitPriceMinor: 5000 })]) }));
  });

  it("removes an added line without submitting a mutation", async () => {
    const user = userEvent.setup(); renderPage();
    await screen.findByDisplayValue("Consulting");
    await user.click(screen.getByRole("button", { name: "Add line" }));
    await user.click(screen.getAllByRole("button", { name: "Remove line" })[1]);
    expect(screen.getAllByLabelText("Line description")).toHaveLength(1);
    expect(mocks.updateDraftInvoice).not.toHaveBeenCalled();
  });
});
