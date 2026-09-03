/* ==========================================================================
   Reports screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  var TYPE_LABELS = { profit_loss:"Profit & Loss", cash_flow:"Cash Flow", project:"Project Financials", expense_breakdown:"Expense Breakdown" };
  var PERIOD_OPTIONS = [
    { value:"2026-09", label:"September 2026" }, { value:"2026-08", label:"August 2026" },
    { value:"2026-Q3", label:"Q3 2026" }, { value:"2026-Q2", label:"Q2 2026" }, { value:"2026-YTD", label:"2026 Year to date" }
  ];

  function periodRange(period){
    if(period==="2026-YTD") return { start:"2026-01-01", end:"2026-12-31" };
    if(period==="2026-Q3") return { start:"2026-07-01", end:"2026-09-30" };
    if(period==="2026-Q2") return { start:"2026-04-01", end:"2026-06-30" };
    return { start: period+"-01", end: period+"-31" };
  }

  function openGenerateModal(onDone){
    var projects = DB.all("projects");
    var formApi;
    var handle = UI.openModal({
      title:"Generate a new report",
      render: function(body){
        var fields = [
          { key:"title", label:"Report title", full:true, placeholder:"e.g. September 2026 Profit & Loss", hint:"Leave blank to auto-generate a title" },
          { key:"type", label:"Report type", type:"select", options:[{value:"profit_loss",label:"Profit & Loss"},{value:"cash_flow",label:"Cash Flow"},{value:"expense_breakdown",label:"Expense Breakdown"},{value:"project",label:"Project Financials"}] },
          { key:"period", label:"Period", type:"select", options: PERIOD_OPTIONS },
          { key:"project_id", label:"Project (for Project Financials)", type:"select", options:[{value:"",label:"— None —"}].concat(projects.map(function(p){return {value:p.id,label:p.name};})) }
        ];
        formApi = UI.renderForm(body, fields, { title:"", type:"profit_loss", period:"2026-09" });
      },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label:"Generate report", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            var v = form.getValues();
            var title = v.title || (TYPE_LABELS[v.type] + " — " + (PERIOD_OPTIONS.find(function(p){return p.value===v.period;})||{}).label);
            var report = buildReport(title, v.type, v.period, v.project_id||null);
            DB.create("reports", report, "rep");
            UI.toast("Report generated", "success");
            close();
            onDone && onDone();
          } }
      ]
    });
  }

  function buildReport(title, type, period, projectId){
    var range = periodRange(period);
    var expenses = DB.all("expenses").filter(function(e){ return e.date>=range.start && e.date<=range.end && e.status==="paid" && (!projectId || e.project_id===projectId); });
    var revenues = DB.all("revenues").filter(function(r){ return r.date>=range.start && r.date<=range.end && r.status==="received" && (!projectId || r.project_id===projectId); });
    var totalExp = U.sumBy(expenses, function(e){return e.amount;});
    var totalRev = U.sumBy(revenues, function(r){return r.amount;});
    var net = totalRev-totalExp;
    var summary = type==="expense_breakdown"
      ? "Total expenses of "+U.money(totalExp)+" recorded across "+expenses.length+" entries, led by "+(topCategory(expenses)||"—")+"."
      : type==="project"
        ? "Project net position of "+U.moneySigned(net)+" with "+U.money(totalRev)+" billed and "+U.money(totalExp)+" spent."
        : "Net "+(net>=0?"profit":"loss")+" of "+U.money(Math.abs(net))+" — "+U.money(totalRev)+" in revenue against "+U.money(totalExp)+" in expenses.";
    return { title:title, type:type, period:period, project_id:projectId, summary:summary, _totals:{ revenue:totalRev, expense:totalExp, net:net }, _range:range };
  }
  function topCategory(expenses){
    var byCat = U.groupBy(expenses, function(e){return e.category;});
    var best = null, bestVal=0;
    Object.keys(byCat).forEach(function(c){ var v = U.sumBy(byCat[c],function(e){return e.amount;}); if(v>bestVal){bestVal=v;best=c;} });
    return best;
  }

  function openDetail(item, onDone){
    UI.openDrawer({
      title: item.title,
      subtitle: TYPE_LABELS[item.type] + (item.project_id ? " · " + DB.projectName(item.project_id) : ""),
      render: function(body){
        var t = item._totals || recomputeTotals(item);
        body.innerHTML =
          '<div class="kv-list" style="margin-bottom:16px">' +
            kv("Generated", U.formatDate(item.created_at)) +
            kv("Period", periodLabel(item.period)) +
            kv("Scope", item.project_id ? DB.projectName(item.project_id) : "All projects") +
          '</div>' +
          '<div class="grid grid-3" style="margin-bottom:16px">' +
            miniStat("Revenue", U.money(t.revenue), "icon-bg-green") +
            miniStat("Expenses", U.money(t.expense), "icon-bg-red") +
            miniStat("Net", U.moneySigned(t.net), "icon-bg-brand") +
          '</div>' +
          '<div id="rep-chart" class="card-pad card" style="margin-bottom:16px"></div>' +
          '<div class="muted" style="margin-bottom:6px">Summary</div>' +
          '<p style="font-size:13px;line-height:1.6">'+U.escapeHtml(item.summary)+'</p>';
        var chartWrap = body.querySelector("#rep-chart");
        UI.donutChart(chartWrap, [ {label:"Revenue", value:Math.max(t.revenue,0.01), color:"var(--brand-500)"}, {label:"Expenses", value:Math.max(t.expense,0.01), color:"var(--red-500)"} ]);
        var legend = document.createElement("div");
        legend.className = "chart-legend";
        legend.style.justifyContent = "center";
        legend.innerHTML = '<span class="item"><span class="sw" style="background:var(--brand-500)"></span>Revenue '+U.money(t.revenue)+'</span><span class="item"><span class="sw" style="background:var(--red-500)"></span>Expenses '+U.money(t.expense)+'</span>';
        chartWrap.style.display = "flex"; chartWrap.style.flexDirection="column"; chartWrap.style.alignItems="center";
        chartWrap.appendChild(legend);
      },
      footer: [
        { label:"Delete", cls:"btn-danger", icon:"trash", onClick: function(close){
          close();
          UI.confirmDialog({ title:"Delete report?", message:'Delete "'+item.title+'"?', confirmLabel:"Delete", danger:true })
            .then(function(ok){ if(ok){ DB.remove("reports", item.id); UI.toast("Report deleted","success"); onDone && onDone(); } });
        }},
        { label:"Download PDF", cls:"btn-primary", icon:"download", onClick: function(){ UI.toast("PDF export is a prototype placeholder — no file is generated.", "info"); } }
      ]
    });
  }
  function recomputeTotals(item){
    var range = item._range || periodRange(item.period);
    var expenses = DB.all("expenses").filter(function(e){ return e.date>=range.start && e.date<=range.end && e.status==="paid" && (!item.project_id || e.project_id===item.project_id); });
    var revenues = DB.all("revenues").filter(function(r){ return r.date>=range.start && r.date<=range.end && r.status==="received" && (!item.project_id || r.project_id===item.project_id); });
    var revenue = U.sumBy(revenues,function(r){return r.amount;}), expense = U.sumBy(expenses,function(e){return e.amount;});
    return { revenue:revenue, expense:expense, net:revenue-expense };
  }
  function periodLabel(p){ var o = PERIOD_OPTIONS.find(function(x){return x.value===p;}); return o?o.label:p; }
  function kv(k,v){ return '<div class="kv-row"><span class="k">'+k+'</span><span class="v">'+v+'</span></div>'; }
  function miniStat(label,value,bg){
    return '<div class="card stat-card" style="padding:12px 14px"><div class="stat-top"><span class="stat-label">'+label+'</span></div><div class="stat-value" style="font-size:16px">'+value+'</div></div>';
  }

  function openEditModal(item, onDone){
    var formApi;
    var handle = UI.openModal({
      title:"Rename report",
      render: function(body){ formApi = UI.renderForm(body, [{key:"title", label:"Report title", required:true, full:true}], item); },
      footer: [
        { label:"Cancel", cls:"btn-ghost", onClick: function(close){ close(); } },
        { label:"Save changes", cls:"btn-primary", onClick: function(close){
            var form = formApi;
            if(!form.validate()) return;
            DB.update("reports", item.id, { title: form.getValues().title });
            UI.toast("Report renamed", "success");
            close(); onDone && onDone();
          } }
      ]
    });
  }

  function render(container){
    UI.renderListScreen(container, {
      heading:"Reports",
      subheading:"Generate and review financial reports.",
      data: function(){ return DB.all("reports"); },
      useProjectFilter:false,
      searchPlaceholder:"Search reports…",
      searchKeys:["title"],
      emptyIcon:"reports", emptyMsg:"No reports yet", emptySub:"Generate your first report to get started.",
      filters:[
        { key:"type", label:"Type", options: Object.keys(TYPE_LABELS).map(function(k){return {value:k,label:TYPE_LABELS[k]};}) }
      ],
      defaultSort:{key:"created_at", dir:"desc"},
      columns:[
        { key:"title", label:"Report", sortable:true, render:function(r){ return '<div class="cell-title">'+U.escapeHtml(r.title)+'</div><div class="cell-sub">'+(r.project_id?DB.projectName(r.project_id):"All projects")+'</div>'; } },
        { key:"type", label:"Type", sortable:true, render:function(r){ return UI.badge(TYPE_LABELS[r.type]||r.type, "brand"); } },
        { key:"period", label:"Period", render:function(r){ return periodLabel(r.period); } },
        { key:"created_at", label:"Generated", sortable:true, render:function(r){ return U.formatDateShort(r.created_at); } }
      ],
      primaryAction:{ label:"Generate report", icon:"plus", onClick: function(refresh){ openGenerateModal(refresh); } },
      onRowClick: function(item, refresh){ openDetail(item, refresh); },
      onEdit: function(item, refresh){ openEditModal(item, refresh); },
      onDelete: function(item, refresh){
        UI.confirmDialog({ title:"Delete report?", message:'Delete "'+item.title+'"?', confirmLabel:"Delete", danger:true })
          .then(function(ok){ if(ok){ DB.remove("reports", item.id); UI.toast("Report deleted","success"); refresh(); } });
      }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.reports = {
    title:"Reports",
    subtitle:"Financial reports and analysis",
    render: render
  };
})();
