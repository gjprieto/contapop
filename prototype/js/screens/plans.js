/* ==========================================================================
   Plans screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function planTotals(plan){
    var rev = U.sumBy(plan.planned_revenues||[], function(x){return x.amount;});
    var exp = U.sumBy(plan.planned_expenses||[], function(x){return x.amount;});
    return { rev:rev, exp:exp, net:rev-exp };
  }

  function openPlanForm(plan, onDone){
    var editing = !!plan;
    var projects = DB.all("projects");
    var formApi;
    var handle = UI.openModal({
      title: editing ? "Edit plan" : "New plan",
      render: function(body){
        var fields = [
          { key:"title", label:"Plan title", required:true, full:true, placeholder:"e.g. Q4 2026 Growth Plan" },
          { key:"description", label:"Description", type:"textarea", full:true, placeholder:"What is this plan for?" },
          { key:"project_id", label:"Project", type:"select", options:[{value:"",label:"General (no project)"}].concat(projects.map(function(p){return {value:p.id,label:p.name};})) },
          { key:"status", label:"Status", type:"select", options:[{value:"draft",label:"Draft"},{value:"active",label:"Active"},{value:"archived",label:"Archived"}] }
        ];
        formApi = UI.renderForm(body, fields, plan || { status:"draft", project_id:"" });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label: editing?"Save changes":"Create plan", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var v = form.getValues();
            v.project_id = v.project_id || null;
            if(editing){ DB.update("plans", plan.id, v); UI.toast("Plan updated","success"); }
            else { v.planned_revenues=[]; v.planned_expenses=[]; DB.create("plans", v, "plan"); UI.toast("Plan created","success"); }
            close(); onDone && onDone();
          } }
      ]
    });
  }

  function openAddLineModal(plan, kind, onDone){
    var isRevenue = kind==="planned_revenues";
    var cats = isRevenue ? H.REVENUE_CATEGORIES : H.EXPENSE_CATEGORIES;
    var formApi;
    var handle = UI.openModal({
      title: isRevenue ? "Add planned revenue" : "Add planned expense",
      render: function(body){
        var fields = [
          { key:"category", label:"Category", type:"select", options:cats },
          { key:"amount", label:"Amount (€)", type:"currency", required:true },
          { key:"date", label:"Expected date", type:"date", required:true },
          { key:"recurring", label:"Recurring monthly", type:"checkbox" }
        ];
        formApi = UI.renderForm(body, fields, { date:H.today(), category:cats[0] });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label:"Add", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var v = form.getValues();
            v.id = U.uid(isRevenue?"prev":"pexp");
            v.recurring_interval = v.recurring ? "monthly" : null;
            plan[kind] = plan[kind] || [];
            plan[kind].push(v);
            DB.update("plans", plan.id, {});
            close();
            UI.toast("Added to plan", "success");
            onDone && onDone();
          } }
      ]
    });
  }

  function openPlanDetail(plan, onListRefresh){
    function paint(){
      var t = planTotals(plan);
      UI.openModal({
        title: plan.title,
        size:"lg",
        render: function(body){
          body.innerHTML =
            '<div style="margin-bottom:14px">' + UI.statusBadge(plan.status) + '</div>' +
            (plan.description ? '<p class="muted" style="margin-bottom:14px">'+U.escapeHtml(plan.description)+'</p>' : '') +
            '<div class="grid grid-3" style="margin-bottom:18px">' +
              mini("Planned revenue", U.money(t.rev), "icon-bg-green") +
              mini("Planned expenses", U.money(t.exp), "icon-bg-red") +
              mini("Planned net", U.moneySigned(t.net), "icon-bg-brand") +
            '</div>' +
            section("Planned revenues", "planned_revenues", plan.planned_revenues) +
            section("Planned expenses", "planned_expenses", plan.planned_expenses);
          U.qsa("[data-add]", body).forEach(function(b){
            b.addEventListener("click", function(){
              UI.closeModal();
              openAddLineModal(plan, b.getAttribute("data-add"), function(){ paint(); onListRefresh && onListRefresh(); });
            });
          });
          U.qsa("[data-remove-line]", body).forEach(function(b){
            b.addEventListener("click", function(){
              var kind = b.getAttribute("data-kind"), id = b.getAttribute("data-remove-line");
              plan[kind] = (plan[kind]||[]).filter(function(x){return x.id!==id;});
              DB.update("plans", plan.id, {});
              UI.closeModal();
              paint();
              onListRefresh && onListRefresh();
            });
          });
        },
        footer: [
          { label:"Delete plan", cls:"btn-danger", icon:"trash", onClick: function(close){
            close();
            UI.confirmDialog({ title:"Delete plan?", message:'Delete "'+plan.title+'" and all its planned entries?', confirmLabel:"Delete", danger:true })
              .then(function(ok){ if(ok){ DB.remove("plans", plan.id); UI.toast("Plan deleted","success"); onListRefresh && onListRefresh(); } });
          }},
          { label:"Edit details", cls:"btn", icon:"edit", onClick: function(close){ close(); openPlanForm(plan, function(){ onListRefresh && onListRefresh(); }); } },
          { label:"Close", cls:"btn-primary", onClick: function(close){ close(); } }
        ]
      });
    }
    paint();

    function mini(label,val,bg){ return '<div class="card stat-card" style="padding:12px 14px"><div class="stat-label">'+label+'</div><div class="stat-value" style="font-size:16px">'+val+'</div></div>'; }
    function section(title, kind, rows){
      rows = rows||[];
      var html = '<div class="flex-between" style="margin-bottom:8px"><span style="font-weight:700;font-size:13px">'+title+'</span><button class="btn btn-sm" data-add="'+kind+'">'+UI.icon("plus")+'<span>Add</span></button></div>';
      if(rows.length===0){ html += '<div class="muted" style="padding:10px 0 18px">No entries yet.</div>'; return html; }
      html += '<div class="table-wrap" style="margin-bottom:18px"><table><thead><tr><th>Category</th><th>Date</th><th class="num">Amount</th><th></th></tr></thead><tbody>';
      rows.forEach(function(r){
        html += '<tr><td>'+U.escapeHtml(r.category)+(r.recurring?' '+UI.badge("Monthly","brand"):'')+'</td><td>'+U.formatDateShort(r.date)+'</td><td class="num">'+U.money(r.amount)+'</td><td><button class="icon-btn btn-sm" data-remove-line="'+r.id+'" data-kind="'+kind+'">'+UI.icon("trash")+'</button></td></tr>';
      });
      html += '</tbody></table></div>';
      return html;
    }
  }

  function render(container){
    UI.renderListScreen(container, {
      heading:"Plans",
      subheading:"Financial strategies and forecasts.",
      data: function(){ return DB.all("plans"); },
      useProjectFilter:true,
      searchPlaceholder:"Search plans…",
      searchKeys:["title","description"],
      emptyIcon:"plans", emptyMsg:"No plans yet", emptySub:"Create your first financial plan.",
      filters:[
        { key:"status", label:"Status", options:[{value:"draft",label:"Draft"},{value:"active",label:"Active"},{value:"archived",label:"Archived"}] }
      ],
      defaultSort:{key:"created_at", dir:"desc"},
      columns:[
        { key:"title", label:"Plan", sortable:true, render:function(p){ return '<div class="cell-title">'+U.escapeHtml(p.title)+'</div><div class="cell-sub">'+DB.projectName(p.project_id)+'</div>'; } },
        { key:"status", label:"Status", sortable:true, render:function(p){ return UI.statusBadge(p.status); } },
        { key:"net", label:"Planned net", align:"right", sortValue:function(p){return planTotals(p).net;}, render:function(p){ var t=planTotals(p); return '<span style="color:'+(t.net>=0?"var(--green-500)":"var(--red-500)")+';font-weight:600">'+U.moneySigned(t.net)+'</span>'; } },
        { key:"created_at", label:"Created", sortable:true, render:function(p){ return U.formatDateShort(p.created_at); } }
      ],
      primaryAction:{ label:"New plan", icon:"plus", onClick: function(refresh){ openPlanForm(null, refresh); } },
      onRowClick: function(item, refresh){ openPlanDetail(item, refresh); },
      onEdit: function(item, refresh){ openPlanForm(item, refresh); },
      onDelete: function(item, refresh){
        UI.confirmDialog({ title:"Delete plan?", message:'Delete "'+item.title+'"?', confirmLabel:"Delete", danger:true })
          .then(function(ok){ if(ok){ DB.remove("plans", item.id); UI.toast("Plan deleted","success"); refresh(); } });
      }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.plans = {
    title:"Plans",
    subtitle:"Financial forecasts and strategies",
    render: render
  };
})();
