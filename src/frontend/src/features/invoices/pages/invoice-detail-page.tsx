import type { ReactNode } from "react";
import { useState } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { useInvoice } from "../api/invoices-queries";
import { downloadInvoiceAttachment, removeInvoiceAttachment, uploadInvoiceAttachment } from "../api/invoices-api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { invoiceKeys } from "../api/invoices-queries";

const money = (amount: number) =>
  new Intl.NumberFormat("es-ES", { style: "currency", currency: "EUR" }).format(amount / 100);

function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return <div className="kv-row"><span className="k">{label}</span><span className="v">{value}</span></div>;
}

export function InvoiceDetailPage() {
  const { invoiceId = "" } = useParams();
  const location = useLocation();
  const invoice = useInvoice(invoiceId);
  const [confirmRemoval, setConfirmRemoval] = useState(false);
  const cache = useQueryClient();
  const refresh = async () => { await cache.invalidateQueries({ queryKey: invoiceKeys.all }); };
  const upload = useMutation({ mutationFn: (file: File) => uploadInvoiceAttachment(invoiceId, file), onSuccess: refresh });
  const remove = useMutation({ mutationFn: () => removeInvoiceAttachment(invoiceId), onSuccess: async () => { setConfirmRemoval(false); await refresh(); } });
  const returnTo = `/invoices${location.search}`;

  if (invoice.isPending) return <main className="page"><p role="status">Loading invoice...</p></main>;
  if (invoice.isError || !invoice.data) return <main className="page"><p role="alert">We could not load this invoice.</p><Link className="btn mt-16" to={returnTo}>Back to invoices</Link></main>;

  const detail = invoice.data;
  return <main className="page">
    <div className="page-head">
      <div><p className="eyebrow">Billing</p><h1>Invoice details</h1><p className="page-intro">{detail.counterpartyName} · {detail.direction} {detail.type}</p></div>
      <Link className="btn" to={returnTo}>Back to invoices</Link>
    </div>
    <div className="grid grid-2">
      <section className="card card-pad"><h2 className="section-title">Invoice</h2><div className="kv-list"><DetailRow label="Counterparty" value={detail.counterpartyName} /><DetailRow label="Direction" value={detail.direction} /><DetailRow label="Type" value={detail.type} /><DetailRow label="Status" value={<span className={`badge badge-${detail.status === "paid" ? "green" : detail.status === "overdue" ? "red" : "amber"}`}>{detail.status}</span>} /><DetailRow label="Invoice date" value={detail.date} /><DetailRow label="Due date" value={detail.dueDate} /></div></section>
      <section className="card card-pad"><h2 className="section-title">Amounts</h2><div className="kv-list"><DetailRow label="Net" value={money(detail.netAmountMinor)} /><DetailRow label="VAT" value={money(detail.taxAmountMinor)} /><DetailRow label="Total" value={money(detail.totalAmountMinor)} /></div></section>
    </div>
    <section className="card mt-16"><div className="card-head"><h2>Line items</h2></div><div className="table-wrap"><table><thead><tr><th>Description</th><th className="num">Quantity</th><th className="num">Unit price</th><th className="num">VAT</th><th className="num">Net</th><th className="num">Total</th></tr></thead><tbody>{detail.lines.map(line => <tr key={line.invoiceLineId}><td className="cell-title">{line.description}</td><td className="num">{line.quantity}</td><td className="num">{money(line.unitPriceMinor)}</td><td className="num">{`${line.taxRate * 100}%`}</td><td className="num">{money(line.netAmountMinor)}</td><td className="num">{money(line.totalAmountMinor)}</td></tr>)}</tbody></table></div></section>
    <section className="card mt-16"><div className="card-head"><h2>Payment history</h2></div>{detail.payments.length === 0 ? <div className="card-body"><p className="muted">No payments recorded.</p></div> : <div className="table-wrap"><table><thead><tr><th>Date</th><th>Method</th><th className="num">Amount</th><th>Reconciliation</th></tr></thead><tbody>{detail.payments.map(payment => <tr key={payment.paymentId}><td>{payment.date}</td><td>{payment.paymentMethod}</td><td className="num">{money(payment.amountMinor)}</td><td>{payment.reconciledTransactionId ? "Reconciled" : "Not reconciled"}</td></tr>)}</tbody></table></div>}</section>
    <section className="card mt-16"><div className="card-head"><h2>Source attachment</h2></div><div className="card-body">{detail.attachment ? <><p>{detail.attachment.fileName} ({Math.ceil(detail.attachment.sizeBytes / 1024)} KB)</p><div className="row-actions visible-actions"><button className="btn btn-sm" type="button" onClick={async () => { const blob = await downloadInvoiceAttachment(invoiceId); window.open(URL.createObjectURL(blob), "_blank", "noopener"); }}>Open attachment</button><label className="btn btn-sm">Replace attachment<input className="visually-hidden" type="file" accept="application/pdf,image/png,image/jpeg" onChange={(event) => { const file = event.currentTarget.files?.[0]; if (file) upload.mutate(file); }} /></label><button className="btn btn-sm" type="button" disabled={remove.isPending} onClick={() => setConfirmRemoval(true)}>Remove attachment</button></div></> : <label className="btn btn-sm">Upload attachment<input className="visually-hidden" type="file" accept="application/pdf,image/png,image/jpeg" onChange={(event) => { const file = event.currentTarget.files?.[0]; if (file) upload.mutate(file); }} /></label>}{upload.isError && <p role="alert">The attachment could not be uploaded. Use a PDF, PNG, or JPEG no larger than 10 MB.</p>}</div></section>
    {confirmRemoval && <div className="modal-backdrop" role="presentation"><section className="modal" role="dialog" aria-modal="true" aria-labelledby="remove-attachment-title"><div className="modal-head"><h2 id="remove-attachment-title">Remove source attachment?</h2></div><div className="modal-body"><p>The uploaded source document will be removed. The generated invoice PDF is unaffected.</p></div><div className="modal-foot"><button className="btn" type="button" disabled={remove.isPending} onClick={() => setConfirmRemoval(false)}>Cancel</button><button className="btn btn-primary" type="button" disabled={remove.isPending} onClick={() => remove.mutate()}>Remove attachment</button></div></section></div>}
  </main>;
}
