/* ==========================================================================
   Expenses screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function formFields(){
    var projectOpts = [{value:"",label:"General (no project)"}].concat(DB.all("projects").map(function(p){return {value:p.id,label:p.name};}));
    return [
      { key:"description", label:"Description", required:true, full:true, placeholder:"e.g. Adobe Creative Cloud" },
      { key:"category", label:"Category", type:"select", required:true, options:H.EXPENSE_CATEGORIES },
      { key:"amount", label:"Amount (€)", type:"currency", required:true, placeholder:"0.00" },
      { key:"date", label:"Date", type:"date", required:true },
      { key:"status", label:"Status", type:"select", options:["paid","pending","overdue"] },
      { key:"project_id", label:"Project", type:"select", options:projectOpts },
      { key:"payment_method", label:"Payment method", type:"select", options:["Business Checking","Visa Debit","Mastercard Corporate","Tax Reserve Savings"] },
      { key:"recurring", label:"This is a recurring expense", type:"checkbox" },
      { key:"notes", label:"Notes", type:"textarea", full:true, placeholder:"Optional notes…" }
    ];
  }

  function openForm(item, onDone){
    var editing = !!item;
    var formApi;
    var handle = UI.openModal({
      title: editing ? "Edit expense" : "New expense",
      render: function(body){
        formApi = UI.renderForm(body, formFields(), item || { date: H.today(), status:"paid", category:H.EXPENSE_CATEGORIES[0], project_id:"" });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label: editing?"Save changes":"Create expense", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var vals = form.getValues();
            vals.project_id = vals.project_id || null;
            vals.recurring_interval = vals.recurring ? "monthly" : null;
            if(editing){ DB.update("expenses", item.id, vals); UI.toast("Expense updated", "success"); }
            else { DB.create("expenses", vals, "exp"); UI.toast("Expense created", "success"); }
            close();
            onDone && onDone();
          } }
      ]
    });
  }

  function openDetail(item, onDone){
    UI.openDrawer({
      title: item.description,
      subtitle: item.category,
      render: function(body){
        body.innerHTML =
          '<div style="margin-bottom:16px">' + UI.statusBadge(item.status) + (item.recurring ? ' ' + UI.badge("Recurring · monthly","brand") : '') + '</div>' +
          '<div class="kv-list">' +
            kv("Amount", U.money(item.amount)) +
            kv("Date", U.formatDate(item.date)) +
            kv("Category", item.category) +
            kv("Project", DB.projectName(item.project_id)) +
            kv("Payment method", item.payment_method || "—") +
            kv("Created", U.formatDate(item.created_at)) +
          '</div>' +
          (item.notes ? '<div class="divider"></div><div class="muted" style="margin-bottom:6px">Notes</div><p style="font-size:13px">'+U.escapeHtml(item.notes)+'</p>' : '') +
          '<div class="divider"></div><div class="muted" style="margin-bottom:8px">History</div>' +
          '<div class="timeline">' +
            '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">Expense recorded</div><div class="d">'+U.formatDate(item.created_at)+'</div></div></div>' +
            (item.status==="paid" ? '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">Marked as paid</div><div class="d">'+U.formatDate(item.date)+'</div></div></div>' : '') +
          '</div>';
      },
      footer: [
        { label:"Delete", cls:"btn-danger", icon:"trash", onClick: function(close){ close(); confirmDelete(item, onDone); } },
        { label:"Edit", cls:"btn-primary", icon:"edit", onClick: function(close){ close(); openForm(item, onDone); } }
      ]
    });
  }
  function kv(k,v){ return '<div class="kv-row"><span class="k">'+k+'</span><span class="v">'+v+'</span></div>'; }

  function confirmDelete(item, onDone){
    UI.confirmDialog({
      title:"Delete expense?",
      message:'Delete "'+item.description+'" ('+U.money(item.amount)+')? This cannot be undone.',
      confirmLabel:"Delete", danger:true
    }).then(function(ok){
      if(ok){ DB.remove("expenses", item.id); UI.toast("Expense deleted", "success"); onDone && onDone(); }
    });
  }

  function openImportCsv(onDone){
    UI.quickImportModal({
      title:"Import expenses from CSV/Excel",
      subtitle:"Upload a spreadsheet exported from your bank or bookkeeping tool.",
      accept:".csv,.xls,.xlsx",
      acceptHint:".csv, .xls, .xlsx up to 10MB",
      dropLabel:"Click to choose a CSV or Excel file",
      defaultFileName:"expenses_export.csv",
      onImport: function(){
        var samples = [
          {description:"Cloud backup subscription", category:"Software & Tools", amount:9.99},
          {description:"Client lunch meeting", category:"Travel", amount:42.50},
          {description:"Domain renewal", category:"Software & Tools", amount:14.00}
        ];
        samples.forEach(function(s){
          DB.create("expenses", Object.assign({date:H.today(), status:"paid", project_id:null, payment_method:"Business Checking", notes:"Imported"}, s), "exp");
        });
        return samples.length;
      },
      afterImport: onDone
    });
  }

  function openImportInvoice(onDone){
    UI.quickImportModal({
      title:"Import expense from invoice (PDF)",
      subtitle:"Upload a supplier invoice or receipt — we'll extract the amount, date and category.",
      accept:".pdf",
      acceptHint:".pdf up to 15MB",
      dropLabel:"Click to choose a PDF invoice",
      confirmLabel:"Extract & import",
      defaultFileName:"supplier_invoice.pdf",
      onImport: function(fileName){
        DB.create("expenses", {
          description:"Extracted from " + fileName, category:"Professional Services", amount: 186.40,
          date:H.today(), status:"pending", project_id:null, payment_method:"Business Checking",
          notes:"Auto-extracted from PDF import — please verify category and amount."
        }, "exp");
        return 1;
      },
      afterImport: onDone
    });
  }

  function render(container){
    var listApi = UI.renderListScreen(container, {
      heading:"Expenses",
      subheading:"Track and manage every outgoing cost.",
      data: function(){ return DB.all("expenses"); },
      useProjectFilter: true,
      searchPlaceholder:"Search expenses…",
      searchKeys:["description","category"],
      emptyIcon:"expenses", emptyMsg:"No expenses found", emptySub:"Try clearing filters or add a new expense.",
      filters:[
        { key:"status", label:"Status", options:[{value:"paid",label:"Paid"},{value:"pending",label:"Pending"},{value:"overdue",label:"Overdue"}] },
        { key:"category", label:"Category", options: H.EXPENSE_CATEGORIES.map(function(c){return {value:c,label:c};}) }
      ],
      defaultSort:{key:"date", dir:"desc"},
      columns:[
        { key:"description", label:"Description", sortable:true, render:function(i){ return '<div class="cell-title">'+U.escapeHtml(i.description)+'</div><div class="cell-sub">'+DB.projectName(i.project_id)+'</div>'; } },
        { key:"category", label:"Category", sortable:true, render:function(i){ return U.escapeHtml(i.category); } },
        { key:"amount", label:"Amount", sortable:true, align:"right", render:function(i){ return U.money(i.amount); } },
        { key:"date", label:"Date", sortable:true, render:function(i){ return U.formatDateShort(i.date); } },
        { key:"status", label:"Status", sortable:true, render:function(i){ return UI.statusBadge(i.status); } }
      ],
      primaryAction:{ label:"New expense", icon:"plus", onClick: function(refresh){ openForm(null, function(){ refresh(); }); } },
      secondaryActions:[
        { label:"Import CSV/Excel", icon:"upload", onClick: function(refresh){ openImportCsv(refresh); } },
        { label:"Import invoice (PDF)", icon:"fileText", onClick: function(refresh){ openImportInvoice(refresh); } }
      ],
      onRowClick: function(item, refresh){ openDetail(item, refresh); },
      onEdit: function(item, refresh){ openForm(item, refresh); },
      onDelete: function(item, refresh){ confirmDelete(item, refresh); }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.expenses = {
    title:"Expenses",
    subtitle:"All outgoing costs across your projects",
    render: render
  };
})();
