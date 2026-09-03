/* ==========================================================================
   Invoices screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, H = window.Contapop.helpers;

  function computeTotals(items){
    var subtotal = U.sumBy(items, function(it){ return (Number(it.qty)||0) * (Number(it.unit_price)||0); });
    var tax = Math.round(subtotal*0.21*100)/100;
    var total = Math.round((subtotal+tax)*100)/100;
    return { subtotal:subtotal, tax:tax, amount:total };
  }

  function openForm(item, onDone){
    var editing = !!item;
    var items = editing ? JSON.parse(JSON.stringify(item.items)) : [{ name:"", qty:1, unit_price:0 }];
    var projects = DB.all("projects");

    var handle = UI.openModal({
      title: editing ? "Edit invoice " + item.number : "New invoice",
      size:"lg",
      render: function(body){ paint(body); }
    });

    function paint(body){
      var v = item || {};
      var html = '<div class="form-grid">' +
        field("Client", '<input id="f-client" placeholder="Client or supplier name" value="'+U.escapeHtml(v.client||"")+'">') +
        field("Project", '<select id="f-project"><option value="">General (no project)</option>' + projects.map(function(p){return '<option value="'+p.id+'" '+(v.project_id===p.id?"selected":"")+'>'+U.escapeHtml(p.name)+'</option>';}).join("") + '</select>') +
        field("Direction", '<select id="f-direction"><option value="outgoing" '+(v.direction!=="incoming"?"selected":"")+'>Outgoing (I am billing)</option><option value="incoming" '+(v.direction==="incoming"?"selected":"")+'>Incoming (I am being billed)</option></select>') +
        field("Type", '<select id="f-type"><option value="service" '+(v.type!=="product"?"selected":"")+'>Service</option><option value="product" '+(v.type==="product"?"selected":"")+'>Product</option></select>') +
        field("Issue date", '<input type="date" id="f-date" value="'+(v.date||H.today())+'">') +
        field("Due date", '<input type="date" id="f-due" value="'+(v.due_date||H.addDays(H.today(),30))+'">') +
        field("Status", '<select id="f-status">' + ["draft","unpaid","paid","overdue"].map(function(s){return '<option value="'+s+'" '+(v.status===s?"selected":"")+'>'+U.titleCase(s)+'</option>';}).join("") + '</select>') +
      '</div>';
      html += '<div class="divider"></div><div class="flex-between" style="margin-bottom:10px"><span style="font-weight:700;font-size:13px">Line items</span><button class="btn btn-sm" id="add-item">'+UI.icon("plus")+'<span>Add item</span></button></div>';
      html += '<div id="items-wrap"></div>';
      html += '<div style="display:flex;justify-content:flex-end;margin-top:14px"><div style="width:220px" class="kv-list" id="totals-box"></div></div>';
      body.innerHTML = html;
      renderItems(body);
      renderTotals(body);

      body.querySelector("#add-item").addEventListener("click", function(){
        items.push({ name:"", qty:1, unit_price:0 });
        renderItems(body); renderTotals(body);
      });
      ["f-client","f-project","f-direction","f-type","f-date","f-due","f-status"].forEach(function(id){});
    }

    function renderItems(body){
      var wrap = body.querySelector("#items-wrap");
      wrap.innerHTML = items.map(function(it,i){
        return '<div class="flex gap-8" style="margin-bottom:8px;align-items:center" data-row="'+i+'">' +
          '<input placeholder="Item description" data-f="name" style="flex:1" value="'+U.escapeHtml(it.name)+'">' +
          '<input type="number" min="1" step="1" data-f="qty" style="width:60px" value="'+it.qty+'">' +
          '<input type="number" min="0" step="0.01" data-f="unit_price" style="width:100px" value="'+it.unit_price+'">' +
          '<span style="width:80px;text-align:right;font-size:12.5px;font-weight:600" data-line-total>'+U.money(it.qty*it.unit_price)+'</span>' +
          '<button class="icon-btn btn-sm" data-remove>'+UI.icon("trash")+'</button>' +
        '</div>';
      }).join("");
      U.qsa("[data-row]", wrap).forEach(function(row){
        var idx = Number(row.getAttribute("data-row"));
        U.qsa("[data-f]", row).forEach(function(inp){
          inp.addEventListener("input", function(){
            var f = inp.getAttribute("data-f");
            items[idx][f] = f==="name" ? inp.value : Number(inp.value)||0;
            row.querySelector("[data-line-total]").textContent = U.money(items[idx].qty*items[idx].unit_price);
            renderTotals(body);
          });
        });
        row.querySelector("[data-remove]").addEventListener("click", function(){
          if(items.length<=1){ UI.toast("An invoice needs at least one line item", "error"); return; }
          items.splice(idx,1); renderItems(body); renderTotals(body);
        });
      });
    }
    function renderTotals(body){
      var t = computeTotals(items);
      body.querySelector("#totals-box").innerHTML =
        '<div class="kv-row"><span class="k">Subtotal</span><span class="v">'+U.money(t.subtotal)+'</span></div>' +
        '<div class="kv-row"><span class="k">VAT (21%)</span><span class="v">'+U.money(t.tax)+'</span></div>' +
        '<div class="kv-row"><span class="k" style="font-weight:700;color:var(--text)">Total</span><span class="v" style="font-size:15px">'+U.money(t.amount)+'</span></div>';
    }

    var footer = document.createElement("div");
    footer.className = "modal-foot";
    var cancel = document.createElement("button"); cancel.className="btn btn-ghost"; cancel.textContent="Cancel";
    cancel.addEventListener("click", UI.closeModal);
    var save = document.createElement("button"); save.className="btn btn-primary"; save.textContent = editing?"Save changes":"Create invoice";
    save.addEventListener("click", function(){
      var body = handle.bodyEl;
      var client = body.querySelector("#f-client").value.trim();
      if(!client){ UI.toast("Client name is required", "error"); return; }
      if(items.some(function(it){ return !it.name.trim(); })){ UI.toast("Every line item needs a description", "error"); return; }
      var totals = computeTotals(items);
      var payload = {
        client: client,
        project_id: body.querySelector("#f-project").value || null,
        direction: body.querySelector("#f-direction").value,
        type: body.querySelector("#f-type").value,
        date: body.querySelector("#f-date").value,
        due_date: body.querySelector("#f-due").value,
        status: body.querySelector("#f-status").value,
        items: items,
        subtotal: totals.subtotal, tax: totals.tax, amount: totals.amount
      };
      if(editing){ DB.update("invoices", item.id, payload); UI.toast("Invoice updated", "success"); }
      else {
        payload.number = "INV-2026-" + (1100 + DB.all("invoices").length);
        DB.create("invoices", payload, "inv");
        UI.toast("Invoice created", "success");
      }
      UI.closeModal();
      onDone && onDone();
    });
    footer.appendChild(cancel); footer.appendChild(save);
    handle.modalEl.appendChild(footer);

    function field(label, inputHtml){
      return '<div class="form-field"><label>'+label+'</label>'+inputHtml+'</div>';
    }
  }

  function directionBadge(d){ return d==="outgoing" ? UI.badge("Outgoing","blue") : UI.badge("Incoming","brand"); }

  function openDetail(item, onDone){
    UI.openDrawer({
      title: item.number,
      subtitle: item.client,
      render: function(body){
        var itemsHtml = '<div class="table-wrap"><table><thead><tr><th>Item</th><th class="num">Qty</th><th class="num">Unit</th><th class="num">Total</th></tr></thead><tbody>' +
          item.items.map(function(it){
            return '<tr><td>'+U.escapeHtml(it.name)+'</td><td class="num">'+it.qty+'</td><td class="num">'+U.money(it.unit_price)+'</td><td class="num">'+U.money(it.qty*it.unit_price)+'</td></tr>';
          }).join("") + '</tbody></table></div>';

        body.innerHTML =
          '<div style="margin-bottom:16px">' + UI.statusBadge(item.status) + ' ' + directionBadge(item.direction) + ' ' + UI.badge(U.titleCase(item.type),"gray") + '</div>' +
          '<div class="kv-list">' +
            kv("Project", DB.projectName(item.project_id)) +
            kv("Issue date", U.formatDate(item.date)) +
            kv("Due date", U.formatDate(item.due_date)) +
          '</div>' +
          '<div class="divider"></div>' + itemsHtml +
          '<div style="display:flex;justify-content:flex-end;margin-top:12px"><div style="width:220px" class="kv-list">' +
            kv("Subtotal", U.money(item.subtotal)) + kv("VAT (21%)", U.money(item.tax)) +
            '<div class="kv-row"><span class="k" style="font-weight:700;color:var(--text)">Total</span><span class="v" style="font-size:15px">'+U.money(item.amount)+'</span></div>' +
          '</div></div>';
      },
      footer: footerFor(item, onDone)
    });
  }
  function kv(k,v){ return '<div class="kv-row"><span class="k">'+k+'</span><span class="v">'+v+'</span></div>'; }

  function footerFor(item, onDone){
    var btns = [];
    if(item.status==="unpaid" || item.status==="overdue"){
      btns.push({ label:"Record payment", cls:"btn", icon:"payments", onClick: function(close){
        close();
        window.Contapop.screens.payments.openCreateForInvoice(item, onDone);
      }});
    }
    if(item.status!=="paid"){
      btns.push({ label:"Mark as paid", cls:"btn-primary", icon:"check", onClick: function(close){
        DB.update("invoices", item.id, { status:"paid" });
        UI.toast("Invoice marked as paid", "success");
        close(); onDone && onDone();
      }});
    }
    btns.push({ label:"Edit", cls: item.status==="paid" ? "btn-primary" : "btn", icon:"edit", onClick: function(close){ close(); openForm(item, onDone); } });
    btns.push({ label:"Delete", cls:"btn-danger", icon:"trash", onClick: function(close){
      close();
      UI.confirmDialog({ title:"Delete invoice?", message:"Delete "+item.number+" for "+item.client+"? This cannot be undone.", confirmLabel:"Delete", danger:true })
        .then(function(ok){ if(ok){ DB.remove("invoices", item.id); UI.toast("Invoice deleted", "success"); onDone && onDone(); } });
    }});
    return btns;
  }

  function render(container){
    UI.renderListScreen(container, {
      heading:"Invoices",
      subheading:"Outgoing and incoming invoices, by status and type.",
      data: function(){ return DB.all("invoices"); },
      useProjectFilter: true,
      searchPlaceholder:"Search invoices or clients…",
      searchKeys:["number","client"],
      emptyIcon:"invoices", emptyMsg:"No invoices found", emptySub:"Try clearing filters or create a new invoice.",
      filters:[
        { key:"status", label:"Status", options:[{value:"paid",label:"Paid"},{value:"unpaid",label:"Unpaid"},{value:"overdue",label:"Overdue"},{value:"draft",label:"Draft"}] },
        { key:"direction", label:"Direction", options:[{value:"outgoing",label:"Outgoing"},{value:"incoming",label:"Incoming"}] },
        { key:"type", label:"Type", options:[{value:"service",label:"Service"},{value:"product",label:"Product"}] }
      ],
      defaultSort:{key:"date", dir:"desc"},
      columns:[
        { key:"number", label:"Invoice", sortable:true, render:function(i){ return '<div class="cell-title">'+i.number+'</div><div class="cell-sub">'+U.escapeHtml(i.client)+'</div>'; } },
        { key:"direction", label:"Direction", sortable:true, render:function(i){ return directionBadge(i.direction); } },
        { key:"type", label:"Type", sortable:true, render:function(i){ return U.titleCase(i.type); } },
        { key:"amount", label:"Amount", sortable:true, align:"right", render:function(i){ return U.money(i.amount); } },
        { key:"due_date", label:"Due", sortable:true, render:function(i){ return U.formatDateShort(i.due_date); } },
        { key:"status", label:"Status", sortable:true, render:function(i){ return UI.statusBadge(i.status); } }
      ],
      primaryAction:{ label:"New invoice", icon:"plus", onClick: function(refresh){ openForm(null, refresh); } },
      onRowClick: function(item, refresh){ openDetail(item, refresh); },
      onEdit: function(item, refresh){ openForm(item, refresh); },
      onDelete: function(item, refresh){
        UI.confirmDialog({ title:"Delete invoice?", message:"Delete "+item.number+" for "+item.client+"?", confirmLabel:"Delete", danger:true })
          .then(function(ok){ if(ok){ DB.remove("invoices", item.id); UI.toast("Invoice deleted", "success"); refresh(); } });
      }
    });
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.invoices = {
    title:"Invoices",
    subtitle:"Incoming and outgoing billing documents",
    render: render,
    openDetailById: function(id, onDone){ var item = DB.get("invoices", id); if(item) openDetail(item, onDone); }
  };
})();
