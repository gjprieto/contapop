/* ==========================================================================
   Revenues screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function formFields(){
    var projectOpts = [{value:"",label:"General (no project)"}].concat(DB.all("projects").map(function(p){return {value:p.id,label:p.name};}));
    return [
      { key:"description", label:"Description", required:true, full:true, placeholder:"e.g. Web Development — Acme Retail" },
      { key:"category", label:"Category", type:"select", required:true, options:H.REVENUE_CATEGORIES },
      { key:"amount", label:"Amount (€)", type:"currency", required:true, placeholder:"0.00" },
      { key:"date", label:"Date", type:"date", required:true },
      { key:"status", label:"Status", type:"select", options:["received","pending","overdue"] },
      { key:"project_id", label:"Project", type:"select", options:projectOpts },
      { key:"recurring", label:"This is a recurring revenue", type:"checkbox" }
    ];
  }

  function openForm(item, onDone){
    var editing = !!item;
    var formApi;
    var handle = UI.openModal({
      title: editing ? "Edit revenue" : "New revenue",
      render: function(body){
        formApi = UI.renderForm(body, formFields(), item || { date: H.today(), status:"received", category:H.REVENUE_CATEGORIES[0], project_id:"" });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label: editing?"Save changes":"Create revenue", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var vals = form.getValues();
            vals.project_id = vals.project_id || null;
            vals.recurring_interval = vals.recurring ? "monthly" : null;
            if(editing){ DB.update("revenues", item.id, vals); UI.toast("Revenue updated", "success"); }
            else { DB.create("revenues", vals, "rev"); UI.toast("Revenue created", "success"); }
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
            kv("Created", U.formatDate(item.created_at)) +
          '</div>' +
          '<div class="divider"></div><div class="muted" style="margin-bottom:8px">History</div>' +
          '<div class="timeline">' +
            '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">Revenue logged</div><div class="d">'+U.formatDate(item.created_at)+'</div></div></div>' +
            (item.status==="received" ? '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">Marked as received</div><div class="d">'+U.formatDate(item.date)+'</div></div></div>' : '') +
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
      title:"Delete revenue?",
      message:'Delete "'+item.description+'" ('+U.money(item.amount)+')? This cannot be undone.',
      confirmLabel:"Delete", danger:true
    }).then(function(ok){
      if(ok){ DB.remove("revenues", item.id); UI.toast("Revenue deleted", "success"); onDone && onDone(); }
    });
  }

  function openImportCsv(onDone){
    UI.quickImportModal({
      title:"Import revenues from CSV/Excel",
      subtitle:"Upload a spreadsheet of income records exported from another tool.",
      accept:".csv,.xls,.xlsx",
      dropLabel:"Click to choose a CSV or Excel file",
      defaultFileName:"revenues_export.csv",
      onImport: function(){
        var samples = [
          { description:"Consulting — Nord Consulting", category:"Consulting", amount:840 },
          { description:"Licensing — template pack", category:"Licensing", amount:220 }
        ];
        samples.forEach(function(s){
          DB.create("revenues", Object.assign({date:H.today(), status:"received", project_id:null}, s), "rev");
        });
        return samples.length;
      },
      afterImport: onDone
    });
  }
  function openImportInvoice(onDone){
    UI.quickImportModal({
      title:"Import revenue from invoice (PDF)",
      subtitle:"Upload an outgoing invoice PDF — we'll extract the client, amount and due date.",
      accept:".pdf",
      dropLabel:"Click to choose a PDF invoice",
      confirmLabel:"Extract & import",
      defaultFileName:"invoice_1082.pdf",
      onImport: function(fileName){
        DB.create("revenues", {
          description:"Extracted from " + fileName, category:"Web Development", amount: 1450,
          date:H.today(), status:"pending", project_id:null
        }, "rev");
        return 1;
      },
      afterImport: onDone
    });
  }

  function render(container){
    UI.renderListScreen(container, {
      heading:"Revenues",
      subheading:"Track and manage every source of income.",
      data: function(){ return DB.all("revenues"); },
      useProjectFilter: true,
      searchPlaceholder:"Search revenues…",
      searchKeys:["description","category"],
      emptyIcon:"revenues", emptyMsg:"No revenues found", emptySub:"Try clearing filters or add a new revenue.",
      filters:[
        { key:"status", label:"Status", options:[{value:"received",label:"Received"},{value:"pending",label:"Pending"},{value:"overdue",label:"Overdue"}] },
        { key:"category", label:"Category", options: H.REVENUE_CATEGORIES.map(function(c){return {value:c,label:c};}) }
      ],
      defaultSort:{key:"date", dir:"desc"},
      columns:[
        { key:"description", label:"Description", sortable:true, render:function(i){ return '<div class="cell-title">'+U.escapeHtml(i.description)+'</div><div class="cell-sub">'+DB.projectName(i.project_id)+'</div>'; } },
        { key:"category", label:"Category", sortable:true, render:function(i){ return U.escapeHtml(i.category); } },
        { key:"amount", label:"Amount", sortable:true, align:"right", render:function(i){ return U.money(i.amount); } },
        { key:"date", label:"Date", sortable:true, render:function(i){ return U.formatDateShort(i.date); } },
        { key:"status", label:"Status", sortable:true, render:function(i){ return UI.statusBadge(i.status); } }
      ],
      primaryAction:{ label:"New revenue", icon:"plus", onClick: function(refresh){ openForm(null, function(){ refresh(); }); } },
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
  window.Contapop.screens.revenues = {
    title:"Revenues",
    subtitle:"All income across your projects",
    render: render
  };
})();
