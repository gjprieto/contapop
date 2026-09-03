/* ==========================================================================
   Financial Overview screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function byProject(items){
    var pid = DB.prefs().currentProjectId;
    if(!pid || pid==="all") return items;
    return items.filter(function(x){ return x.project_id===pid; });
  }

  function render(container){
    var revenues = byProject(DB.all("revenues"));
    var expenses = byProject(DB.all("expenses"));
    var ytdRevenue = U.sumBy(revenues.filter(function(r){return r.status==="received";}), function(r){return r.amount;});
    var ytdExpense = U.sumBy(expenses.filter(function(e){return e.status==="paid";}), function(e){return e.amount;});

    container.innerHTML =
      '<div class="page-head"><div><div class="section-title">2026 year-to-date</div><p class="muted">Revenue, expenses and budget health across the year.</p></div>'+
      '<div class="page-head-actions"><button class="btn" id="btn-import">'+UI.icon("upload")+'<span>Import bank statement</span></button>'+
      '<button class="btn" id="btn-export">'+UI.icon("download")+'<span>Export summary</span></button></div></div>'+
      '<div class="grid grid-3" style="margin-bottom:18px">'+
        statCard("Total revenue", U.money(ytdRevenue), "revenues", "icon-bg-green")+
        statCard("Total expenses", U.money(ytdExpense), "expenses", "icon-bg-red")+
        statCard("Net profit", U.moneySigned(ytdRevenue-ytdExpense), "overview", "icon-bg-brand")+
      '</div>'+
      '<div class="grid grid-2" style="align-items:start;gap:16px;grid-template-columns:1.5fr 1fr">'+
        '<div class="card"><div class="card-head"><h3>Revenue vs. expenses by month</h3></div><div class="card-body" id="ov-chart"></div></div>'+
        '<div class="card"><div class="card-head"><h3>Expense breakdown</h3></div><div class="card-body" id="ov-donut"></div></div>'+
      '</div>'+
      '<div class="grid grid-2" style="align-items:start;gap:16px;margin-top:16px">'+
        '<div class="card"><div class="card-head"><h3>Budget status — September</h3><button class="link-btn" data-nav="expenses">View expenses</button></div><div class="card-body" id="ov-budgets"></div></div>'+
        '<div class="card"><div class="card-head"><h3>Jump to</h3></div><div class="card-body"><div style="display:flex;flex-direction:column;gap:6px">'+
          navBtn("expenses","Expenses")+navBtn("revenues","Revenues")+navBtn("invoices","Invoices")+navBtn("payments","Payments")+
        '</div></div></div>'+
      '</div>';

    function statCard(label,value,iconName,bg){
      return '<div class="card stat-card"><div class="stat-top"><span class="stat-label">'+label+'</span><span class="stat-icon '+bg+'">'+UI.icon(iconName)+'</span></div><div class="stat-value">'+value+'</div></div>';
    }
    function navBtn(route,label){
      return '<button class="btn" style="justify-content:flex-start" data-nav="'+route+'">'+UI.icon(route)+'<span>'+label+'</span></button>';
    }

    // monthly chart
    var months = H.MONTHS_2026;
    var revSeries = months.map(function(m){
      var key = "2026-"+String(m).padStart(2,"0");
      return { x: H.MONTH_NAMES[m-1], y: U.sumBy(revenues.filter(function(r){return r.date.slice(0,7)===key && r.status==="received";}), function(r){return r.amount;}) };
    });
    var expSeries = months.map(function(m){
      var key = "2026-"+String(m).padStart(2,"0");
      return { x: H.MONTH_NAMES[m-1], y: U.sumBy(expenses.filter(function(e){return e.date.slice(0,7)===key && e.status==="paid";}), function(e){return e.amount;}) };
    });
    UI.monthlyBarChart(U.byId("ov-chart"), [
      { label:"Revenue", data:revSeries, color:"var(--brand-500)" },
      { label:"Expenses", data:expSeries, color:"var(--red-500)" }
    ]);

    // donut by category
    var byCat = U.groupBy(expenses.filter(function(e){return e.status==="paid";}), function(e){return e.category;});
    var donutData = Object.keys(byCat).map(function(cat,i){
      return { label:cat, value: U.sumBy(byCat[cat], function(e){return e.amount;}), color: UI.PALETTE[i%UI.PALETTE.length] };
    }).sort(function(a,b){return b.value-a.value;});
    var donutWrap = U.byId("ov-donut");
    var chartRow = document.createElement("div");
    chartRow.style.cssText = "display:flex;align-items:center;gap:18px;flex-wrap:wrap;justify-content:center";
    var chartHolder = document.createElement("div");
    donutWrap.appendChild(chartRow);
    chartRow.appendChild(chartHolder);
    UI.donutChart(chartHolder, donutData);
    var legend = document.createElement("div");
    legend.innerHTML = donutData.slice(0,6).map(function(d){
      return '<div class="chart-legend" style="margin:2px 0"><span class="item"><span class="sw" style="background:'+d.color+'"></span>'+U.escapeHtml(d.label)+' — '+U.money(d.value)+'</span></div>';
    }).join("");
    chartRow.appendChild(legend);

    // budgets
    var budgets = DB.all("budgets");
    var budgetsEl = U.byId("ov-budgets");
    budgets.forEach(function(b){
      var spent = U.sumBy(expenses.filter(function(e){return e.category===b.category && e.date>=b.start_date && e.date<=b.end_date && e.status==="paid";}), function(e){return e.amount;});
      var pct = Math.round((spent/b.allocated_amount)*100);
      var row = document.createElement("div");
      row.style.marginBottom = "12px";
      row.innerHTML = '<div class="flex-between" style="margin-bottom:5px"><span style="font-size:12.5px;font-weight:600">'+U.escapeHtml(b.name)+'</span><span style="font-size:12px;color:var(--text-faint)">'+U.money(spent)+' / '+U.money(b.allocated_amount)+'</span></div>'+
        '<div class="track" style="height:8px;border-radius:99px;background:var(--surface-2);overflow:hidden"><div style="height:100%;width:'+U.clamp(pct,0,100)+'%;border-radius:99px;background:'+(pct>=100?"var(--red-500)":pct>=80?"var(--amber-500)":"var(--brand-500)")+'"></div></div>';
      budgetsEl.appendChild(row);
    });

    U.qsa("[data-nav]", container).forEach(function(el){
      el.addEventListener("click", function(){ window.Contapop.router.navigate(el.getAttribute("data-nav")); });
    });

    U.byId("btn-export").addEventListener("click", function(){
      UI.toast("Summary export queued — this is a prototype, no file is generated.", "info");
    });
    U.byId("btn-import").addEventListener("click", openImportWizard);
  }

  /* ---------------- import wizard ---------------- */
  function openImportWizard(){
    var accounts = DB.all("bankAccounts");
    var step = 1;
    var chosenAccount = accounts[0].id;
    var fileName = null;
    var mapping = { date:"Date", description:"Description", amount:"Amount", type:"Auto-detect from sign" };
    var mockRows = [
      { Date:"2026-09-01", Description:"CLIENT PAYMENT - NORD CONSULT", Amount:"2200.00" },
      { Date:"2026-09-01", Description:"ADOBE CREATIVE CLOUD", Amount:"-59.99" },
      { Date:"2026-08-30", Description:"COWORKING MEMBERSHIP", Amount:"-180.00" },
      { Date:"2026-08-29", Description:"TRANSFER - ACME RETAIL INV 1076", Amount:"1450.00" },
      { Date:"2026-08-28", Description:"MOBILE PHONE PLAN", Amount:"-32.50" }
    ];

    var modalHandle = UI.openModal({
      title:"Import bank statement",
      size:"lg",
      dismissable:true
    });
    renderStep(modalHandle.bodyEl);

    function renderStep(body){
      var html = '<div class="step-indicator">' + [1,2,3].map(function(n,i){
        var cls = n===step ? "active" : n<step ? "done" : "";
        var labels = ["Upload file","Map columns","Review & confirm"];
        return '<div class="step '+cls+'"><span class="num">'+(n<step?"✓":n)+'</span><span>'+labels[i]+'</span></div>' + (n<3?'<div class="sep"></div>':'');
      }).join("") + '</div>';

      if(step===1){
        html += '<div class="form-field full" style="margin-bottom:16px"><label>Destination account</label><select id="wiz-account">' +
          accounts.map(function(a){ return '<option value="'+a.id+'" '+(a.id===chosenAccount?"selected":"")+'>'+U.escapeHtml(a.name)+' — '+U.escapeHtml(a.bank_name)+'</option>'; }).join("") + '</select></div>';
        html += '<div class="dropzone" id="wiz-dropzone">'+UI.icon("upload")+'<div style="font-weight:600;color:var(--text)">'+(fileName?U.escapeHtml(fileName):"Click to choose a CSV or Excel file")+'</div><div class="sub" style="font-size:11.5px;margin-top:4px">.csv, .xls, .xlsx up to 10MB</div><input type="file" id="wiz-file" accept=".csv,.xls,.xlsx" style="display:none"></div>';
      } else if(step===2){
        html += '<p class="muted" style="margin-bottom:12px">We detected the following columns in your file. Map each to a Contapop field.</p>';
        html += '<div class="form-grid">';
        ["date","description","amount"].forEach(function(k){
          html += '<div class="form-field"><label>'+U.titleCase(k)+' column</label><select data-map="'+k+'">'+
            ["Date","Description","Amount","Balance","Reference"].map(function(c){ return '<option '+(mapping[k]===c?"selected":"")+'>'+c+'</option>'; }).join("") +
            '</select></div>';
        });
        html += '<div class="form-field"><label>Transaction type</label><select data-map="type"><option selected>Auto-detect from sign</option><option>Use separate Debit/Credit columns</option></select></div>';
        html += '</div>';
        html += '<div class="divider"></div><p class="muted">Preview (first '+mockRows.length+' rows)</p>';
        html += previewTable(mockRows);
      } else if(step===3){
        var acc = accounts.find(function(a){return a.id===chosenAccount;});
        html += '<div class="card-pad card" style="background:var(--brand-50);border:1px solid var(--brand-100);margin-bottom:16px">'+
          '<div class="flex gap-8" style="align-items:center"><span class="stat-icon icon-bg-brand">'+UI.icon("fileText")+'</span><div><div style="font-weight:700;font-size:13.5px">'+(fileName||"bank_statement_sep2026.csv")+'</div><div class="muted">Importing into '+U.escapeHtml(acc.name)+'</div></div></div></div>';
        html += '<div class="kv-list card-pad card">'+
          '<div class="kv-row"><span class="k">Rows detected</span><span class="v">'+mockRows.length+'</span></div>'+
          '<div class="kv-row"><span class="k">New transactions</span><span class="v">'+mockRows.length+'</span></div>'+
          '<div class="kv-row"><span class="k">Possible duplicates</span><span class="v">0</span></div>'+
          '<div class="kv-row"><span class="k">Date range</span><span class="v">Aug 28 – Sep 1, 2026</span></div>'+
        '</div>';
        html += '<div id="wiz-progress-wrap" style="margin-top:16px;display:none"><div class="muted" style="margin-bottom:6px">Importing…</div><div class="progress-bar"><div class="fill" id="wiz-progress-fill" style="width:0%"></div></div></div>';
      }
      body.innerHTML = html;
      wireStep(body);
    }

    function previewTable(rows){
      var h = '<div class="table-wrap"><table><thead><tr><th>Date</th><th>Description</th><th class="num">Amount</th><th>Type</th></tr></thead><tbody>';
      rows.forEach(function(r){
        var amt = Number(r.Amount);
        h += '<tr><td>'+r.Date+'</td><td>'+U.escapeHtml(r.Description)+'</td><td class="num">'+U.moneySigned(amt)+'</td><td>'+(amt>=0?UI.badge("Income","green"):UI.badge("Expense","red"))+'</td></tr>';
      });
      h += '</tbody></table></div>';
      return h;
    }

    function wireStep(body){
      if(step===1){
        var dz = U.qs("#wiz-dropzone", body);
        var fileInput = U.qs("#wiz-file", body);
        dz.addEventListener("click", function(){ fileInput.click(); });
        fileInput.addEventListener("change", function(){
          if(fileInput.files && fileInput.files[0]){ fileName = fileInput.files[0].name; }
          else { fileName = "bank_statement_sep2026.csv"; }
          renderStep(body);
        });
        U.qs("#wiz-account", body).addEventListener("change", function(e){ chosenAccount = e.target.value; });
      }
      if(step===2){
        U.qsa("[data-map]", body).forEach(function(sel){
          sel.addEventListener("change", function(){ mapping[sel.getAttribute("data-map")] = sel.value; });
        });
      }
      renderFooter(body);
    }

    function renderFooter(body){
      var modalEl = modalHandle.modalEl;
      var oldFoot = modalEl.querySelector(".modal-foot");
      if(oldFoot) oldFoot.remove();
      var foot = document.createElement("div");
      foot.className = "modal-foot";
      var backBtn = btn("Back", "btn-ghost", function(){ step--; renderStep(modalHandle.bodyEl); });
      var cancelBtn = btn("Cancel", "btn-ghost", function(){ UI.closeModal(); });
      var nextLabel = step===3 ? "Confirm import" : "Continue";
      var nextBtn = btn(nextLabel, "btn-primary", function(){
        if(step===1 && !fileName){ UI.toast("Choose a file to continue", "error"); return; }
        if(step<3){ step++; renderStep(modalHandle.bodyEl); }
        else { doImport(); }
      });
      if(step>1) foot.appendChild(backBtn); else foot.appendChild(cancelBtn);
      foot.appendChild(nextBtn);
      modalEl.appendChild(foot);
    }
    function btn(label, cls, onClick){
      var b = document.createElement("button");
      b.className = "btn " + cls;
      b.textContent = label;
      b.addEventListener("click", onClick);
      return b;
    }

    function doImport(){
      var wrap = U.byId("wiz-progress-wrap");
      var fill = U.byId("wiz-progress-fill");
      var foot = modalHandle.modalEl.querySelector(".modal-foot");
      if(foot) foot.style.display = "none";
      wrap.style.display = "block";
      var pct = 0;
      var timer = setInterval(function(){
        pct += 20;
        fill.style.width = Math.min(pct,100)+"%";
        if(pct>=100){
          clearInterval(timer);
          mockRows.forEach(function(r){
            var amt = Number(r.Amount);
            DB.create("transactions", {
              tenant_id:"t1", bank_account_id: chosenAccount, amount: amt, date: r.Date,
              type: amt>=0?"income":"expense", description: r.Description, category: amt>=0?"Imported income":"Imported expense",
              reconciled:false
            }, "txn");
          });
          setTimeout(function(){
            UI.closeModal();
            UI.toast(mockRows.length+" transactions imported successfully", "success");
            window.Contapop.router.renderRoute();
          }, 350);
        }
      }, 220);
    }
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.overview = {
    title: "Financial Overview",
    subtitle: "Monitor revenue, expenses and budget health",
    render: render
  };
})();
