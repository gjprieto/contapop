/* ==========================================================================
   Transactions screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function accountName(id){ var a = DB.get("bankAccounts", id); return a ? a.name : "—"; }

  function openForm(item, onDone){
    var editing = !!item;
    var accounts = DB.all("bankAccounts");
    var formApi;
    var handle = UI.openModal({
      title: editing ? "Edit transaction" : "New transaction",
      render: function(body){
        var fields = [
          { key:"description", label:"Description", required:true, full:true },
          { key:"bank_account_id", label:"Account", type:"select", required:true, options: accounts.map(function(a){return {value:a.id,label:a.name};}) },
          { key:"type", label:"Type", type:"select", options:[{value:"income",label:"Income"},{value:"expense",label:"Expense"}] },
          { key:"amount", label:"Amount (€, positive)", type:"currency", required:true },
          { key:"date", label:"Date", type:"date", required:true },
          { key:"category", label:"Category", placeholder:"e.g. Software & Tools" },
          { key:"reconciled", label:"Reconciled", type:"checkbox" }
        ];
        var initial = item ? Object.assign({}, item, { amount: Math.abs(item.amount) }) : { date:H.today(), type:"expense", bank_account_id: accounts[0].id };
        formApi = UI.renderForm(body, fields, initial);
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label: editing?"Save changes":"Create transaction", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var v = form.getValues();
            v.amount = v.type==="expense" ? -Math.abs(v.amount) : Math.abs(v.amount);
            if(editing){ DB.update("transactions", item.id, v); UI.toast("Transaction updated","success"); }
            else { DB.create("transactions", v, "txn"); UI.toast("Transaction created","success"); }
            close(); onDone && onDone();
          } }
      ]
    });
  }

  function openDetail(item, onDone){
    UI.openDrawer({
      title: item.description,
      subtitle: accountName(item.bank_account_id),
      render: function(body){
        body.innerHTML =
          '<div style="margin-bottom:16px">' + (item.reconciled ? UI.badge("Reconciled","green") : UI.badge("Unreconciled","amber")) + ' ' + UI.badge(U.titleCase(item.type), item.type==="income"?"blue":"red") + '</div>' +
          '<div class="kv-list">' +
            kv("Amount", '<span style="color:'+(item.amount<0?"var(--red-500)":"var(--green-500)")+'">'+U.moneySigned(item.amount)+'</span>') +
            kv("Date", U.formatDate(item.date)) +
            kv("Account", accountName(item.bank_account_id)) +
            kv("Category", item.category||"—") +
          '</div>';
      },
      footer: [
        { label:"Delete", cls:"btn-danger", icon:"trash", onClick: function(close){
          close();
          UI.confirmDialog({ title:"Delete transaction?", message:"Delete \""+item.description+"\"? This cannot be undone.", confirmLabel:"Delete", danger:true })
            .then(function(ok){ if(ok){ DB.remove("transactions", item.id); UI.toast("Transaction deleted","success"); onDone && onDone(); } });
        }},
        { label: item.reconciled ? "Mark unreconciled" : "Mark reconciled", cls:"btn", icon:"check", onClick: function(close){
          DB.update("transactions", item.id, { reconciled: !item.reconciled });
          UI.toast(item.reconciled ? "Marked as unreconciled" : "Marked as reconciled", "success");
          close(); onDone && onDone();
        }},
        { label:"Edit", cls:"btn-primary", icon:"edit", onClick: function(close){ close(); openForm(item, onDone); } }
      ]
    });
  }
  function kv(k,v){ return '<div class="kv-row"><span class="k">'+k+'</span><span class="v">'+v+'</span></div>'; }

  function render(container){
    var accounts = DB.all("bankAccounts");
    var head = document.createElement("div");
    head.className = "grid grid-2";
    head.style.marginBottom = "18px";
    accounts.forEach(function(a){
      var bal = a.balance;
      head.innerHTML += '<div class="card stat-card"><div class="stat-top"><span class="stat-label">'+U.escapeHtml(a.name)+' · '+U.escapeHtml(a.bank_name)+'</span><span class="stat-icon icon-bg-brand">'+UI.icon("payments")+'</span></div>'+
        '<div class="stat-value">'+U.money(bal)+'</div><div class="stat-delta" style="color:var(--text-faint)">'+U.escapeHtml(a.account_number)+'</div></div>';
    });
    container.appendChild(head);

    UI.renderListScreen(container, {
      heading:"Transactions",
      subheading:"Every movement across your connected bank accounts.",
      data: function(){ return DB.all("transactions"); },
      useProjectFilter:false,
      searchPlaceholder:"Search transactions…",
      searchKeys:["description","category"],
      emptyIcon:"transactions", emptyMsg:"No transactions found", emptySub:"Try clearing filters, or import a bank statement from Financial Overview.",
      filters:[
        { key:"bank_account_id", label:"Account", options: accounts.map(function(a){return {value:a.id,label:a.name};}) },
        { key:"type", label:"Type", options:[{value:"income",label:"Income"},{value:"expense",label:"Expense"}] }
      ],
      defaultSort:{key:"date", dir:"desc"},
      pageSize:10,
      columns:[
        { key:"description", label:"Description", sortable:true, render:function(t){ return '<div class="cell-title">'+U.escapeHtml(t.description)+'</div><div class="cell-sub">'+accountName(t.bank_account_id)+'</div>'; } },
        { key:"category", label:"Category", render:function(t){ return U.escapeHtml(t.category||"—"); } },
        { key:"amount", label:"Amount", sortable:true, align:"right", render:function(t){ return '<span style="color:'+(t.amount<0?"var(--red-500)":"var(--green-500)")+';font-weight:600">'+U.moneySigned(t.amount)+'</span>'; } },
        { key:"date", label:"Date", sortable:true, render:function(t){ return U.formatDateShort(t.date); } },
        { key:"reconciled", label:"Reconciled", render:function(t){ return t.reconciled ? UI.badge("Reconciled","green") : UI.badge("Unreconciled","amber"); } }
      ],
      primaryAction:{ label:"New transaction", icon:"plus", onClick: function(refresh){ openForm(null, refresh); } },
      onRowClick: function(item, refresh){ openDetail(item, refresh); },
      onEdit: function(item, refresh){ openForm(item, refresh); },
      onDelete: function(item, refresh){
        UI.confirmDialog({ title:"Delete transaction?", message:"Delete \""+item.description+"\"?", confirmLabel:"Delete", danger:true })
          .then(function(ok){ if(ok){ DB.remove("transactions", item.id); UI.toast("Transaction deleted","success"); refresh(); } });
      }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.transactions = {
    title:"Transactions",
    subtitle:"All bank account activity in one place",
    render: render
  };
})();
