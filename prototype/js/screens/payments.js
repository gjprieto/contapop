/* ==========================================================================
   Payments screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function amountPaidForInvoice(invoiceId, excludePaymentId){
    return U.sumBy(DB.all("payments").filter(function(p){ return p.invoice_id===invoiceId && p.id!==excludePaymentId && p.status!=="failed"; }), function(p){return p.amount;});
  }

  function invoiceLabel(inv){
    var paid = amountPaidForInvoice(inv.id);
    var remaining = Math.round((inv.amount-paid)*100)/100;
    return inv.number+" — "+inv.client+" ("+U.money(remaining)+" due)";
  }

  function openForm(payment, presetInvoice, onDone){
    var editing = !!payment;
    var invoices = DB.all("invoices").filter(function(i){ return editing ? true : (i.status!=="paid" && i.status!=="draft"); });
    if(invoices.length===0) invoices = DB.all("invoices");
    var selectedInvoiceId = (payment && payment.invoice_id) || (presetInvoice && presetInvoice.id) || (invoices[0] && invoices[0].id);

    var handle = UI.openModal({
      title: editing ? "Edit payment" : "Record payment",
      render: function(body){
        body.innerHTML =
          '<div class="form-grid">' +
          '<div class="form-field full"><label>Invoice</label><select id="f-invoice">' +
            invoices.map(function(i){ return '<option value="'+i.id+'" '+(i.id===selectedInvoiceId?"selected":"")+'>'+invoiceLabel(i)+'</option>'; }).join("") +
          '</select></div>' +
          '<div class="form-field"><label>Amount (€)</label><input type="number" step="0.01" id="f-amount" value="'+(payment?payment.amount:remainingFor(selectedInvoiceId))+'"></div>' +
          '<div class="form-field"><label>Date</label><input type="date" id="f-date" value="'+(payment?payment.date:H.today())+'"></div>' +
          '<div class="form-field"><label>Payment method</label><select id="f-method">' +
            ["Bank transfer","Credit card","Direct debit","PayPal","Cash"].map(function(m){ return '<option '+(payment&&payment.payment_method===m?"selected":"")+'>'+m+'</option>'; }).join("") +
          '</select></div>' +
          '<div class="form-field"><label>Status</label><select id="f-status">' +
            ["completed","partial","pending","failed"].map(function(s){ return '<option value="'+s+'" '+(payment&&payment.status===s?"selected":(!payment&&s==="completed"?"selected":""))+'>'+U.titleCase(s)+'</option>'; }).join("") +
          '</select></div>' +
          '</div>';
        body.querySelector("#f-invoice").addEventListener("change", function(e){
          body.querySelector("#f-amount").value = remainingFor(e.target.value);
        });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label: editing?"Save changes":"Record payment", cls:"btn-primary", onClick: function(close){
            var body = handle.bodyEl;
            var invId = body.querySelector("#f-invoice").value;
            var amt = Number(body.querySelector("#f-amount").value)||0;
            if(amt<=0){ UI.toast("Enter a valid amount", "error"); return; }
            var inv = DB.get("invoices", invId);
            var payload = {
              invoice_id: invId, project_id: inv?inv.project_id:null,
              amount: amt, date: body.querySelector("#f-date").value,
              payment_method: body.querySelector("#f-method").value,
              status: body.querySelector("#f-status").value
            };
            if(editing){ DB.update("payments", payment.id, payload); UI.toast("Payment updated", "success"); }
            else {
              DB.create("payments", payload, "pay");
              UI.toast("Payment recorded", "success");
              if(inv && payload.status==="completed"){
                var totalPaid = amountPaidForInvoice(invId);
                if(totalPaid >= inv.amount) DB.update("invoices", invId, { status:"paid" });
              }
            }
            close();
            onDone && onDone();
          } }
      ]
    });
    function remainingFor(id){
      var inv = DB.get("invoices", id);
      if(!inv) return 0;
      var paid = amountPaidForInvoice(id, editing?payment.id:null);
      return Math.max(0, Math.round((inv.amount-paid)*100)/100);
    }
  }

  function openDetail(item, onDone){
    var inv = DB.get("invoices", item.invoice_id);
    UI.openDrawer({
      title:"Payment · " + U.money(item.amount),
      subtitle: inv ? inv.number + " — " + inv.client : "Invoice not found",
      render: function(body){
        body.innerHTML =
          '<div style="margin-bottom:16px">' + UI.statusBadge(item.status==="completed"?"paid":item.status) + '</div>' +
          '<div class="kv-list">' +
            kv("Amount", U.money(item.amount)) +
            kv("Date", U.formatDate(item.date)) +
            kv("Method", item.payment_method) +
            kv("Project", DB.projectName(item.project_id)) +
            kv("Invoice", inv ? inv.number : "—") +
          '</div>';
      },
      footer: [
        { label:"Delete", cls:"btn-danger", icon:"trash", onClick: function(close){
          close();
          UI.confirmDialog({ title:"Delete payment?", message:"Delete this payment of "+U.money(item.amount)+"?", confirmLabel:"Delete", danger:true })
            .then(function(ok){ if(ok){ DB.remove("payments", item.id); UI.toast("Payment deleted","success"); onDone && onDone(); } });
        }},
        { label:"View invoice", cls:"btn", icon:"invoices", onClick: function(close){
          close();
          if(inv && window.Contapop.screens.invoices.openDetailById) window.Contapop.screens.invoices.openDetailById(inv.id, onDone);
          else { UI.toast("Invoice not found", "error"); }
        }},
        { label:"Edit", cls:"btn-primary", icon:"edit", onClick: function(close){ close(); openForm(item, null, onDone); } }
      ]
    });
  }
  function kv(k,v){ return '<div class="kv-row"><span class="k">'+k+'</span><span class="v">'+v+'</span></div>'; }

  function render(container){
    UI.renderListScreen(container, {
      heading:"Payments",
      subheading:"Every payment recorded against your invoices.",
      data: function(){ return DB.all("payments"); },
      useProjectFilter: true,
      searchPlaceholder:"Search payments…",
      searchKeys:[function(p){ var inv = DB.get("invoices", p.invoice_id); return inv ? inv.number+" "+inv.client : ""; }],
      emptyIcon:"payments", emptyMsg:"No payments found", emptySub:"Try clearing filters or record a new payment.",
      filters:[
        { key:"status", label:"Status", options:[{value:"completed",label:"Completed"},{value:"partial",label:"Partial"},{value:"pending",label:"Pending"},{value:"failed",label:"Failed"}] },
        { key:"payment_method", label:"Method", options:["Bank transfer","Credit card","Direct debit","PayPal","Cash"].map(function(m){return {value:m,label:m};}) }
      ],
      defaultSort:{key:"date", dir:"desc"},
      columns:[
        { key:"invoice_id", label:"Invoice", render:function(p){ var inv = DB.get("invoices",p.invoice_id); return inv ? '<div class="cell-title">'+inv.number+'</div><div class="cell-sub">'+U.escapeHtml(inv.client)+'</div>' : '<span class="faint">Deleted invoice</span>'; } },
        { key:"amount", label:"Amount", sortable:true, align:"right", render:function(p){ return U.money(p.amount); } },
        { key:"date", label:"Date", sortable:true, render:function(p){ return U.formatDateShort(p.date); } },
        { key:"payment_method", label:"Method", sortable:true, render:function(p){ return U.escapeHtml(p.payment_method); } },
        { key:"status", label:"Status", sortable:true, render:function(p){ return UI.statusBadge(p.status==="completed"?"paid":p.status); } }
      ],
      primaryAction:{ label:"Record payment", icon:"plus", onClick: function(refresh){ openForm(null, null, refresh); } },
      onRowClick: function(item, refresh){ openDetail(item, refresh); },
      onEdit: function(item, refresh){ openForm(item, null, refresh); },
      onDelete: function(item, refresh){
        UI.confirmDialog({ title:"Delete payment?", message:"Delete this payment of "+U.money(item.amount)+"?", confirmLabel:"Delete", danger:true })
          .then(function(ok){ if(ok){ DB.remove("payments", item.id); UI.toast("Payment deleted","success"); refresh(); } });
      }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.payments = {
    title:"Payments",
    subtitle:"Payments recorded against invoices",
    render: render,
    openCreateForInvoice: function(invoice, onDone){ openForm(null, invoice, onDone); }
  };
})();
