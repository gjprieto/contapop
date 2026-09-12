import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { z } from "zod";
import { updateDraftInvoice } from "../api/invoices-api";
import { invoiceKeys, useInvoice } from "../api/invoices-queries";

const lineSchema = z.object({
  description: z.string().trim().min(1, "Enter a line description."),
  quantity: z.coerce.number<number>().int().positive(),
  unitPrice: z.coerce.number<number>().positive(),
  taxRate: z.coerce.number<number>().min(0).max(1),
});
const schema = z.object({
  date: z.string().min(1),
  dueDate: z.string().min(1),
  lines: z.array(lineSchema).min(1, "Add at least one line."),
});
type Values = z.infer<typeof schema>;

const money = (amount: number) =>
  new Intl.NumberFormat("es-ES", { style: "currency", currency: "EUR" }).format(amount / 100);

export function EditInvoicePage() {
  const { invoiceId = "" } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const cache = useQueryClient();
  const invoice = useInvoice(invoiceId);
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const lines = useFieldArray({ control: form.control, name: "lines" });
  const detail = invoice.data;

  useEffect(() => {
    if (detail?.status === "draft") {
      form.reset({
        date: detail.date,
        dueDate: detail.dueDate,
        lines: detail.lines.map((line) => ({
          description: line.description,
          quantity: line.quantity,
          unitPrice: line.unitPriceMinor / 100,
          taxRate: line.taxRate,
        })),
      });
    }
  }, [detail, form]);

  const mutation = useMutation({
    mutationFn: (input: Values) =>
      updateDraftInvoice(detail!, {
        date: input.date,
        dueDate: input.dueDate,
        lines: input.lines.map((line) => ({
          description: line.description,
          quantity: line.quantity,
          unitPriceMinor: Math.round(line.unitPrice * 100),
          taxRate: line.taxRate,
        })),
      }),
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: invoiceKeys.all });
      navigate(`/invoices/${invoiceId}${location.search}`);
    },
  });

  if (invoice.isPending) return <main className="page"><p role="status">Loading invoice...</p></main>;
  if (invoice.isError || !detail || detail.status !== "draft") return <main className="page"><p role="alert">This draft invoice cannot be edited.</p><Link className="btn mt-16" to={`/invoices/${invoiceId}${location.search}`}>Back to invoice</Link></main>;

  const watched = form.watch("lines") ?? [];
  const totals = watched.reduce((total, line) => {
    const net = Math.round((Number(line.quantity) || 0) * (Number(line.unitPrice) || 0) * 100);
    const tax = Math.round(net * (Number(line.taxRate) || 0));
    return { net: total.net + net, tax: total.tax + tax };
  }, { net: 0, tax: 0 });
  const backToDetail = `/invoices/${invoiceId}${location.search}`;

  return <main className="page">
    <div className="page-head">
      <div><p className="eyebrow">Billing</p><h1>Edit draft invoice</h1><p className="page-intro">{detail.counterpartyName} · {detail.direction} · {detail.type}</p></div>
      <Link className="btn" to={backToDetail}>Back</Link>
    </div>
    <form className="card card-pad" noValidate onSubmit={form.handleSubmit((input) => mutation.mutate(input))}>
      <div className="form-grid">
        <div className="form-field"><label htmlFor="invoice-date">Invoice date</label><input id="invoice-date" type="date" {...form.register("date")} /></div>
        <div className="form-field"><label htmlFor="invoice-due-date">Due date</label><input id="invoice-due-date" type="date" {...form.register("dueDate")} /></div>
      </div>
      <section className="invoice-line-editor mt-16" aria-labelledby="invoice-lines-title">
        <div className="card-head"><h2 id="invoice-lines-title">Line items</h2></div>
        <div className="invoice-line-header" aria-hidden="true"><span>Line description</span><span>Quantity</span><span>Unit price</span><span>VAT</span></div>
        <div role="group" aria-label="Invoice item lines">
          {lines.fields.map((field, index) => <div className="invoice-line-grid" key={field.id}>
            <label className="visually-hidden" htmlFor={`line-${index}-description`}>Line description</label><input id={`line-${index}-description`} {...form.register(`lines.${index}.description`)} />
            <label className="visually-hidden" htmlFor={`line-${index}-quantity`}>Quantity</label><input id={`line-${index}-quantity`} type="number" min="1" {...form.register(`lines.${index}.quantity`)} />
            <label className="visually-hidden" htmlFor={`line-${index}-price`}>Unit price (EUR)</label><input id={`line-${index}-price`} type="number" min="0.01" step="0.01" {...form.register(`lines.${index}.unitPrice`)} />
            <label className="visually-hidden" htmlFor={`line-${index}-tax`}>VAT rate</label><input id={`line-${index}-tax`} type="number" min="0" max="1" step="0.01" {...form.register(`lines.${index}.taxRate`)} />
          </div>)}
        </div>
        <div className="invoice-line-actions">
          <button className="btn btn-sm" type="button" disabled={lines.fields.length === 1} onClick={() => lines.remove(lines.fields.length - 1)}>Remove line</button>
          <button className="btn btn-sm" type="button" onClick={() => lines.append({ description: "", quantity: 1, unitPrice: 0, taxRate: 0.21 })}>Add line</button>
        </div>
      </section>
      <p className="invoice-totals">Net {money(totals.net)} · VAT {money(totals.tax)} · Total {money(totals.net + totals.tax)}</p>
      {mutation.isError && <p role="alert">We could not update this invoice.</p>}
      <div className="modal-foot"><Link className="btn" to={backToDetail}>Cancel</Link><button className="btn btn-primary" type="submit" disabled={mutation.isPending}>{mutation.isPending ? "Saving..." : "Save changes"}</button></div>
    </form>
  </main>;
}
