/* ==========================================================================
   Home screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB;

  function byProject(items){
    var pid = DB.prefs().currentProjectId;
    if(!pid || pid==="all") return items;
    return items.filter(function(x){ return x.project_id===pid; });
  }

  function render(container){
    var user = DB.currentUser();
    var month = "2026-09";
    var revenues = byProject(DB.all("revenues"));
    var expenses = byProject(DB.all("expenses"));
    var invoices = byProject(DB.all("invoices"));

    var mtdRevenue = U.sumBy(revenues.filter(function(r){return r.date.slice(0,7)===month && r.status==="received";}), function(r){return r.amount;});
    var mtdExpense = U.sumBy(expenses.filter(function(e){return e.date.slice(0,7)===month && e.status==="paid";}), function(e){return e.amount;});
    var prevMonth = "2026-08";
    var prevRevenue = U.sumBy(revenues.filter(function(r){return r.date.slice(0,7)===prevMonth && r.status==="received";}), function(r){return r.amount;});
    var prevExpense = U.sumBy(expenses.filter(function(e){return e.date.slice(0,7)===prevMonth && e.status==="paid";}), function(e){return e.amount;});
    var net = mtdRevenue - mtdExpense;
    var outstanding = U.sumBy(invoices.filter(function(i){return i.direction==="outgoing" && (i.status==="unpaid"||i.status==="overdue");}), function(i){return i.amount;});

    function delta(cur,prev){
      if(prev===0) return null;
      return Math.round(((cur-prev)/prev)*1000)/10;
    }
    var revDelta = delta(mtdRevenue, prevRevenue);
    var expDelta = delta(mtdExpense, prevExpense);

    var budgets = DB.all("budgets");
    var budgetUsed = U.sumBy(budgets, function(b){
      var spent = U.sumBy(expenses.filter(function(e){return e.category===b.category && e.date>=b.start_date && e.date<=b.end_date && e.status==="paid";}), function(e){return e.amount;});
      return spent;
    });
    var budgetTotal = U.sumBy(budgets, function(b){return b.allocated_amount;});
    var budgetPct = budgetTotal>0 ? Math.round((budgetUsed/budgetTotal)*100) : 0;

    var overdueInvoices = invoices.filter(function(i){return i.status==="overdue";});

    container.innerHTML =
      '<div class="card-pad card" style="margin-bottom:20px;background:linear-gradient(135deg,var(--brand-500),var(--brand-700));color:#fff;border:none;display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:14px">'+
        '<div><div style="font-size:11px;text-transform:uppercase;letter-spacing:.06em;opacity:.85;font-weight:700">Welcome back</div>'+
        '<div style="font-size:20px;font-weight:700;margin-top:3px">'+U.escapeHtml(user.name)+'</div>'+
        '<div style="font-size:12.5px;opacity:.85;margin-top:4px">Here is what is happening across '+(DB.prefs().currentProjectId==="all"?"all your projects":DB.projectName(DB.prefs().currentProjectId))+' this month.</div></div>'+
        '<div style="display:flex;gap:22px">'+
          '<div><div style="font-size:11px;opacity:.8">Net (Sep)</div><div style="font-size:19px;font-weight:700">'+U.moneySigned(net)+'</div></div>'+
          '<div><div style="font-size:11px;opacity:.8">Outstanding</div><div style="font-size:19px;font-weight:700">'+U.money(outstanding)+'</div></div>'+
        '</div>'+
      '</div>'+
      '<div class="grid grid-4" id="home-stats" style="margin-bottom:20px"></div>'+
      '<div class="grid grid-2" style="align-items:start;gap:16px">'+
        '<div class="card">'+
          '<div class="card-head"><h3>Recent activity</h3><button class="link-btn" data-nav="transactions">View transactions</button></div>'+
          '<div class="card-body" id="home-activity"></div>'+
        '</div>'+
        '<div style="display:flex;flex-direction:column;gap:16px">'+
          '<div class="card">'+
            '<div class="card-head"><h3>Alerts</h3></div>'+
            '<div class="card-body" id="home-alerts"></div>'+
          '</div>'+
          '<div class="card">'+
            '<div class="card-head"><h3>Quick access</h3></div>'+
            '<div class="card-body" id="home-quick"></div>'+
          '</div>'+
        '</div>'+
      '</div>';

    var stats = U.byId("home-stats");
    function statCard(label, value, iconName, iconBg, delta, sub){
      var deltaHtml = "";
      if(delta!==null && delta!==undefined){
        var up = delta>=0;
        deltaHtml = '<div class="stat-delta '+(up?"up":"down")+'">'+(up?"▲":"▼")+' '+Math.abs(delta)+'% vs Aug</div>';
      } else if(sub){
        deltaHtml = '<div class="stat-delta" style="color:var(--text-faint)">'+sub+'</div>';
      }
      return '<div class="card stat-card"><div class="stat-top"><span class="stat-label">'+label+'</span><span class="stat-icon '+iconBg+'">'+UI.icon(iconName)+'</span></div>'+
        '<div class="stat-value">'+value+'</div>'+deltaHtml+'</div>';
    }
    stats.innerHTML =
      statCard("Revenue (Sep)", U.money(mtdRevenue), "revenues", "icon-bg-green", revDelta) +
      statCard("Expenses (Sep)", U.money(mtdExpense), "expenses", "icon-bg-red", expDelta) +
      statCard("Budget used", budgetPct+"%", "target", "icon-bg-amber", null, U.money(budgetUsed)+" of "+U.money(budgetTotal)) +
      statCard("Overdue invoices", String(overdueInvoices.length), "alert", "icon-bg-blue", null, U.money(U.sumBy(overdueInvoices,function(i){return i.amount;}))+" total");

    var activity = DB.all("activity").slice(0,6);
    var activityEl = U.byId("home-activity");
    if(activity.length===0){ activityEl.innerHTML = UI.emptyStateHtml({icon:"activity", msg:"No recent activity"}); }
    else {
      activityEl.innerHTML = '<div class="timeline">' + activity.map(function(a){
        return '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">'+U.escapeHtml(a.text)+'</div><div class="d">'+U.formatDateShort(a.date)+'</div></div></div>';
      }).join("") + '</div>';
    }

    var alertsEl = U.byId("home-alerts");
    var alerts = [];
    if(overdueInvoices.length) alerts.push({icon:"alert", text: overdueInvoices.length+" invoice(s) overdue, totaling "+U.money(U.sumBy(overdueInvoices,function(i){return i.amount;})), color:"icon-bg-red"});
    if(budgetPct>=80) alerts.push({icon:"target", text:"You've used "+budgetPct+"% of your monthly budget", color:"icon-bg-amber"});
    var unread = DB.all("notifications").filter(function(n){return !n.read;});
    if(unread.length) alerts.push({icon:"bell", text: unread.length+" unread notification(s)", color:"icon-bg-blue"});
    if(alerts.length===0){ alertsEl.innerHTML = UI.emptyStateHtml({icon:"checkCircle", msg:"Nothing needs your attention"}); }
    else alertsEl.innerHTML = alerts.map(function(a){
      return '<div style="display:flex;gap:10px;align-items:flex-start;padding:8px 0"><span class="stat-icon '+a.color+'" style="width:26px;height:26px">'+UI.icon(a.icon)+'</span><span style="font-size:12.5px;padding-top:3px">'+a.text+'</span></div>';
    }).join('<div class="divider" style="margin:0"></div>');

    var quickEl = U.byId("home-quick");
    var quicks = [
      {label:"New expense", route:"expenses", icon:"expenses"},
      {label:"New invoice", route:"invoices", icon:"invoices"},
      {label:"View reports", route:"reports", icon:"reports"},
      {label:"Financial overview", route:"overview", icon:"overview"}
    ];
    quickEl.innerHTML = '<div style="display:flex;flex-direction:column;gap:6px">' + quicks.map(function(q){
      return '<button class="btn" style="justify-content:flex-start" data-nav="'+q.route+'">'+UI.icon(q.icon)+'<span>'+q.label+'</span></button>';
    }).join("") + '</div>';

    U.qsa("[data-nav]", container).forEach(function(el){
      el.addEventListener("click", function(){ window.Contapop.router.navigate(el.getAttribute("data-nav")); });
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.home = {
    title: "Home",
    subtitle: "Your financial snapshot at a glance",
    render: render
  };
})();
