import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { Invoice, Paged } from "../types";
import { InvoicesPage } from "./invoices-page";

const mocks = vi.hoisted(() => ({
  getInvoices: vi.fn(),
  getCounterparties: vi.fn(),
  getCurrentUser: vi.fn(),
  createInvoice: vi.fn(),
  changeInvoiceStatus: vi.fn(),
  deleteDraftInvoice: vi.fn(),
  downloadInvoice: vi.fn(),
}));
vi.mock("../api/invoices-api", () => ({
  getInvoices: mocks.getInvoices,
  getCounterparties: mocks.getCounterparties,
  createInvoice: mocks.createInvoice,
  createCounterparty: vi.fn(),
  changeInvoiceStatus: mocks.changeInvoiceStatus,
  deleteDraftInvoice: mocks.deleteDraftInvoice,
  downloadInvoice: mocks.downloadInvoice,
  uploadInvoiceAttachment: vi.fn(),
}));
vi.mock("../../auth/api/auth-api", () => ({
  getCurrentUser: mocks.getCurrentUser,
}));

function LocationDisplay() {
  const location = useLocation();
  return <output data-testid="location">{location.search}</output>;
}
function renderPage(
  initialEntry = "/invoices",
  invoices: Paged<Invoice> = {
    items: [],
    page: 1,
    pageSize: 10,
    totalCount: 0,
  },
) {
  mocks.getInvoices.mockResolvedValue(invoices);
  mocks.getCounterparties.mockResolvedValue({
    items: [
      {
        counterpartyId: "counterparty-1",
        type: "customer",
        name: "Acme SL",
        status: "active",
      },
    ],
    page: 1,
    pageSize: 100,
    totalCount: 1,
  });
  mocks.getCurrentUser.mockResolvedValue({ projectId: "project-1" });
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route
            path="/invoices"
            element={
              <>
                <InvoicesPage />
                <LocationDisplay />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("InvoicesPage", () => {
  it("shows calculated VAT and submits the derived minor-unit line", async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole("button", { name: "Create invoice" }));
    await user.selectOptions(
      await screen.findByLabelText("Counterparty"),
      "counterparty-1",
    );
    await user.type(screen.getByLabelText("Line description"), "Consulting");
    await user.clear(screen.getByLabelText("Unit price (EUR)"));
    await user.type(screen.getByLabelText("Unit price (EUR)"), "100");
    await user.type(screen.getByLabelText("Invoice date"), "2026-09-09");
    await user.type(screen.getByLabelText("Due date"), "2026-10-09");
    expect(
      screen.getByText(/Net 100,00.*VAT 21,00.*Total 121,00/),
    ).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Create draft" }));
    expect(mocks.createInvoice).toHaveBeenCalledWith(
      expect.objectContaining({
        lines: [
          expect.objectContaining({ unitPriceMinor: 10000, taxRate: 0.21 }),
        ],
      }),
    );
  });

  it("synchronizes filters to the URL, converts EUR amounts, and resets pagination", async () => {
    const user = userEvent.setup();
    renderPage("/invoices?page=3");
    await user.selectOptions(
      await screen.findByLabelText("Invoice status"),
      "issued",
    );
    await user.type(
      screen.getByLabelText("Minimum total amount (EUR)"),
      "123.45",
    );
    expect(screen.getByTestId("location")).toHaveTextContent("status=issued");
    expect(screen.getByTestId("location")).toHaveTextContent(
      "totalAmountMinMinor=12345",
    );
    expect(screen.getByTestId("location")).toHaveTextContent("page=1");
    expect(mocks.getInvoices).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: "issued",
        totalAmountMinMinor: 12345,
        page: 1,
      }),
      expect.anything(),
    );
  });

  it("renders filtered results and clears filters back to the default list", async () => {
    const invoice: Invoice = {
      invoiceId: "invoice-1",
      counterpartyId: "counterparty-1",
      counterpartyName: "Acme SL",
      direction: "outgoing",
      type: "service",
      status: "issued",
      canArchive: true,
      netAmountMinor: 10000,
      taxAmountMinor: 2100,
      totalAmountMinor: 12100,
      date: "2026-09-09",
      dueDate: "2026-10-09",
      version: 1,
    };
    const user = userEvent.setup();
    renderPage("/invoices?status=issued&search=Acme&page=2", {
      items: [invoice],
      page: 2,
      pageSize: 10,
      totalCount: 1,
    });
    expect(await screen.findByText("Acme SL")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(screen.getByTestId("location")).toHaveTextContent("");
    expect(mocks.getInvoices).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: undefined,
        search: undefined,
        page: 1,
        sort: "date:desc",
      }),
      expect.anything(),
    );
  });

  it("opens the generated PDF in a new tab from its accessible document action", async () => {
    const invoice: Invoice = {
      invoiceId: "invoice-1",
      counterpartyId: "counterparty-1",
      counterpartyName: "Acme SL",
      direction: "outgoing",
      type: "service",
      status: "issued",
      canArchive: true,
      netAmountMinor: 10000,
      taxAmountMinor: 2100,
      totalAmountMinor: 12100,
      date: "2026-09-09",
      dueDate: "2026-10-09",
      version: 1,
    };
    mocks.downloadInvoice.mockResolvedValue(
      new Blob(["invoice"], { type: "application/pdf" }),
    );
    const createObjectUrl = vi
      .spyOn(URL, "createObjectURL")
      .mockReturnValue("blob:invoice-pdf");
    const open = vi.spyOn(window, "open").mockImplementation(() => null);
    const user = userEvent.setup();
    renderPage("/invoices", {
      items: [invoice],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });
    await user.click(
      await screen.findByRole("button", { name: "Open generated invoice PDF" }),
    );
    expect(open).toHaveBeenCalledWith("blob:invoice-pdf", "_blank", "noopener");
    createObjectUrl.mockRestore();
    open.mockRestore();
  });

  it("opens invoice details while retaining the URL-backed list filters", async () => {
    const invoice: Invoice = {
      invoiceId: "invoice-1",
      counterpartyId: "counterparty-1",
      counterpartyName: "Acme SL",
      direction: "outgoing",
      type: "service",
      status: "issued",
      canArchive: true,
      netAmountMinor: 10000,
      taxAmountMinor: 2100,
      totalAmountMinor: 12100,
      date: "2026-09-09",
      dueDate: "2026-10-09",
      version: 1,
    };
    renderPage("/invoices?status=issued&search=Acme", {
      items: [invoice], page: 1, pageSize: 10, totalCount: 1,
    });

    expect(await screen.findByRole("link", { name: "View invoice details for Acme SL" })).toHaveAttribute(
      "href", "/invoices/invoice-1?status=issued&search=Acme",
    );
  });

  it("only offers archive when allowed and archives after confirmation", async () => {
    const archiveable: Invoice = {
      invoiceId: "archiveable",
      counterpartyId: "counterparty-1",
      counterpartyName: "Acme SL",
      direction: "outgoing",
      type: "service",
      status: "issued",
      canArchive: true,
      netAmountMinor: 10000,
      taxAmountMinor: 2100,
      totalAmountMinor: 12100,
      date: "2026-09-09",
      dueDate: "2026-10-09",
      version: 1,
    };
    const paymentLinked: Invoice = {
      ...archiveable,
      invoiceId: "payment-linked",
      canArchive: false,
    };
    mocks.changeInvoiceStatus.mockResolvedValue({});
    const user = userEvent.setup();
    renderPage("/invoices", {
      items: [archiveable, paymentLinked],
      page: 1,
      pageSize: 10,
      totalCount: 2,
    });

    expect(
      await screen.findAllByRole("button", { name: "Archive" }),
    ).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Archive" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(mocks.changeInvoiceStatus).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Archive" }));
    await user.click(screen.getByRole("button", { name: "Archive invoice" }));
    expect(mocks.changeInvoiceStatus).toHaveBeenCalledWith(
      archiveable,
      "archive",
    );
  });

  it("only offers draft deletion when allowed and deletes after confirmation", async () => {
    const deletable: Invoice = {
      invoiceId: "draft", counterpartyId: "counterparty-1", counterpartyName: "Draft Acme", direction: "outgoing", type: "service", status: "draft", canArchive: false, canDelete: true, netAmountMinor: 10000, taxAmountMinor: 2100, totalAmountMinor: 12100, date: "2026-09-09", dueDate: "2026-10-09", version: 3,
    };
    const paymentLinked = { ...deletable, invoiceId: "payment-linked", counterpartyName: "Paid Draft", canDelete: false };
    mocks.deleteDraftInvoice.mockResolvedValue(undefined);
    const user = userEvent.setup();
    renderPage("/invoices", { items: [deletable, paymentLinked], page: 1, pageSize: 10, totalCount: 2 });

    expect(await screen.findAllByRole("button", { name: "Delete" })).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(mocks.deleteDraftInvoice).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await user.click(screen.getByRole("button", { name: "Delete invoice" }));
    expect(mocks.deleteDraftInvoice).toHaveBeenCalledWith(deletable, expect.anything());
  });
});
