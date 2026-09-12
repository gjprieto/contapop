import { describe, expect, it, vi } from "vitest";
import { deleteDraftInvoice } from "./invoices-api";
import type { Invoice } from "../types";

describe("deleteDraftInvoice", () => {
  it("accepts the API's no-content success response", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(null, { status: 204 }),
    );
    const invoice: Invoice = {
      invoiceId: "invoice-1", counterpartyId: "counterparty-1", counterpartyName: "Acme SL",
      direction: "outgoing", type: "service", status: "draft", canArchive: false, canDelete: true,
      netAmountMinor: 100, taxAmountMinor: 21, totalAmountMinor: 121,
      date: "2026-09-12", dueDate: "2026-10-12", version: 3,
    };

    await expect(deleteDraftInvoice(invoice)).resolves.toBeUndefined();
    expect(fetchMock).toHaveBeenCalledWith(
      "/experience/v1/invoices/invoice-1",
      expect.objectContaining({ method: "DELETE", credentials: "include" }),
    );
    fetchMock.mockRestore();
  });
});
