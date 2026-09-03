/* ==========================================================================
   Contapop prototype — shared UI kit
   icons, modal, drawer, confirm, toast, form builder, list-screen builder,
   simple inline-SVG charts.
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils;
  var DB = window.Contapop.DB;

  /* ---------------- icons ---------------- */
  var ICON_PATHS = {
    home: '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/><path d="M9 20v-6h6v6"/>',
    overview: '<path d="M3 3v18h18"/><path d="M7 15l4-5 3 3 5-7"/>',
    expenses: '<circle cx="12" cy="12" r="9"/><path d="M8 12h8"/>',
    revenues: '<circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/>',
    invoices: '<path d="M6 3h9l5 5v13H6z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h6M9 9h2"/>',
    payments: '<rect x="2.5" y="5.5" width="19" height="14" rx="2"/><path d="M2.5 10h19"/><path d="M6 15h4"/>',
    transactions: '<path d="M7 4v13a2 2 0 002 2h9"/><path d="M17 20l3-3-3-3"/><path d="M17 20V7a2 2 0 00-2-2H6"/><path d="M7 4L4 7l3 3"/>',
    reports: '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M8 16v-4M12 16V8M16 16v-7"/>',
    plans: '<path d="M12 2v4"/><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 00.34 1.87l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.7 1.7 0 00-1.87-.34 1.7 1.7 0 00-1.04 1.56V21a2 2 0 11-4 0v-.09A1.7 1.7 0 009 19.4a1.7 1.7 0 00-1.87.34l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.7 1.7 0 004.6 15a1.7 1.7 0 00-1.56-1.04H3a2 2 0 110-4h.09A1.7 1.7 0 004.6 9a1.7 1.7 0 00-.34-1.87l-.06-.06a2 2 0 112.83-2.83l.06.06A1.7 1.7 0 009 4.6a1.7 1.7 0 001.04-1.56V3a2 2 0 114 0v.09c0 .68.4 1.29 1.04 1.56.66.27 1.4.14 1.87-.34l.06-.06a2 2 0 112.83 2.83l-.06.06c-.48.47-.6 1.2-.34 1.87.27.64.88 1.04 1.56 1.04H21a2 2 0 110 4h-.09c-.68 0-1.29.4-1.51 1.04z"/>',
    user: '<circle cx="12" cy="8" r="4"/><path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>',
    bell: '<path d="M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 01-3.4 0"/>',
    menu: '<path d="M3 6h18M3 12h18M3 18h18"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 013 3L7 19l-4 1 1-4z"/>',
    trash: '<path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/>',
    download: '<path d="M12 3v12"/><path d="M7 10l5 5 5-5"/><path d="M5 21h14"/>',
    upload: '<path d="M12 21V9"/><path d="M7 14l5-5 5 5"/><path d="M5 3h14"/>',
    x: '<path d="M18 6L6 18M6 6l12 12"/>',
    chevronDown: '<path d="M6 9l6 6 6-6"/>',
    chevronLeft: '<path d="M15 18l-6-6 6-6"/>',
    chevronRight: '<path d="M9 18l6-6-6-6"/>',
    check: '<path d="M20 6L9 17l-5-5"/>',
    filter: '<path d="M4 5h16M7 12h10M10 19h4"/>',
    building: '<rect x="4" y="3" width="16" height="18" rx="1"/><path d="M9 8h1M14 8h1M9 12h1M14 12h1M9 16h6"/>',
    logout: '<path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/>',
    moon: '<path d="M20 14.5A8.5 8.5 0 119.5 4a7 7 0 0010.5 10.5z"/>',
    sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 8h.01M11 12h1v5h1"/>',
    alert: '<path d="M10.3 3.86l-8.2 14.2A1 1 0 003 19.5h18a1 1 0 00.87-1.44l-8.2-14.2a1 1 0 00-1.74 0z"/><path d="M12 9v4M12 17h.01"/>',
    dots: '<circle cx="12" cy="5" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="12" cy="19" r="1"/>',
    calendar: '<rect x="3" y="4" width="18" height="17" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/>',
    folder: '<path d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2z"/>',
    link: '<path d="M9 15l6-6"/><path d="M13 5l1.5-1.5a3.5 3.5 0 015 5L18 10"/><path d="M11 19l-1.5 1.5a3.5 3.5 0 01-5-5L6 14"/>',
    fileText: '<path d="M6 2h9l5 5v15H6z"/><path d="M14 2v5h5"/><path d="M9 13h6M9 17h6"/>',
    creditCard: '<rect x="2.5" y="5.5" width="19" height="14" rx="2"/><path d="M2.5 10h19"/>',
    activity: '<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>',
    shield: '<path d="M12 2l8 4v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6z"/>',
    globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a15 15 0 010 18M12 3a15 15 0 000 18"/>',
    checkCircle: '<circle cx="12" cy="12" r="9"/><path d="M8 12l3 3 5-6"/>',
    inbox: '<path d="M3 12h5l2 3h4l2-3h5"/><path d="M5 4h14l2 8v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6z"/>',
    target: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.4"/>'
  };
  function icon(name, cls){
    var p = ICON_PATHS[name] || ICON_PATHS.info;
    return '<svg class="icon '+(cls||"")+'" viewBox="0 0 24 24">'+p+'</svg>';
  }

  /* ---------------- status badge mapping ---------------- */
  var STATUS_MAP = {
    paid:["green","Paid"], received:["green","Received"], completed:["green","Completed"], active:["green","Active"],
    reconciled:["green","Reconciled"],
    unpaid:["amber","Unpaid"], pending:["amber","Pending"], draft:["gray","Draft"], partial:["blue","Partial"],
    overdue:["red","Overdue"], cancelled:["gray","Cancelled"], archived:["gray","Archived"]
  };
  function statusBadge(status){
    var m = STATUS_MAP[status] || ["gray", U.titleCase(status||"—")];
    return '<span class="badge badge-'+m[0]+'">'+m[1]+'</span>';
  }
  function badge(text, color){ return '<span class="badge badge-'+(color||"gray")+'">'+U.escapeHtml(text)+'</span>'; }

  /* ---------------- toast ---------------- */
  function toast(message, type){
    type = type || "success";
    var root = U.byId("toast-root");
    var el = document.createElement("div");
    el.className = "toast " + type;
    var iconName = type==="success"?"checkCircle":type==="error"?"alert":"info";
    el.innerHTML = icon(iconName,"") .replace("<svg ","<svg style=\"width:15px;height:15px;color:inherit\" ") + '<span></span>';
    el.querySelector("span").textContent = message;
    root.appendChild(el);
    setTimeout(function(){
      el.style.transition = "opacity .2s ease";
      el.style.opacity = "0";
      setTimeout(function(){ el.remove(); }, 200);
    }, 3200);
  }

  /* ---------------- modal ---------------- */
  function closeModal(){
    var root = U.byId("modal-root");
    root.innerHTML = "";
  }
  function openModal(opts){
    var root = U.byId("modal-root");
    root.innerHTML = "";
    var backdrop = document.createElement("div");
    backdrop.className = "modal-backdrop";
    backdrop.addEventListener("mousedown", function(e){ if(e.target===backdrop && opts.dismissable!==false) closeModal(); });
    var modal = document.createElement("div");
    modal.className = "modal" + (opts.size==="lg" ? " modal-lg" : "");
    var head = document.createElement("div");
    head.className = "modal-head";
    head.innerHTML = "<h2></h2>";
    head.querySelector("h2").textContent = opts.title || "";
    var closeBtn = document.createElement("button");
    closeBtn.className = "icon-btn"; closeBtn.innerHTML = icon("x");
    closeBtn.addEventListener("click", closeModal);
    head.appendChild(closeBtn);
    var body = document.createElement("div");
    body.className = "modal-body";
    modal.appendChild(head); modal.appendChild(body);
    if(opts.render) opts.render(body);
    else if(opts.html) body.innerHTML = opts.html;
    if(opts.footer && opts.footer.length){
      var foot = document.createElement("div");
      foot.className = "modal-foot";
      opts.footer.forEach(function(b){
        var btn = document.createElement("button");
        btn.className = "btn " + (b.cls||"");
        btn.innerHTML = (b.icon?icon(b.icon):"") + "<span></span>";
        btn.querySelector("span").textContent = b.label;
        btn.addEventListener("click", function(){ b.onClick && b.onClick(closeModal); });
        foot.appendChild(btn);
      });
      modal.appendChild(foot);
    }
    backdrop.appendChild(modal);
    root.appendChild(backdrop);
    return { close: closeModal, bodyEl: body, modalEl: modal };
  }

  /* ---------------- drawer ---------------- */
  function closeDrawer(){
    var root = U.byId("drawer-root");
    root.innerHTML = "";
  }
  function openDrawer(opts){
    var root = U.byId("drawer-root");
    root.innerHTML = "";
    var backdrop = document.createElement("div");
    backdrop.className = "drawer-backdrop";
    backdrop.addEventListener("mousedown", function(e){ if(e.target===backdrop) closeDrawer(); });
    var drawer = document.createElement("div");
    drawer.className = "drawer";
    var head = document.createElement("div");
    head.className = "drawer-head";
    head.innerHTML = '<div><h2></h2><p class="muted" style="margin-top:3px"></p></div>';
    head.querySelector("h2").textContent = opts.title || "";
    if(opts.subtitle) head.querySelector("p").textContent = opts.subtitle;
    var closeBtn = document.createElement("button");
    closeBtn.className = "icon-btn"; closeBtn.innerHTML = icon("x");
    closeBtn.addEventListener("click", closeDrawer);
    head.appendChild(closeBtn);
    var body = document.createElement("div");
    body.className = "drawer-body";
    drawer.appendChild(head); drawer.appendChild(body);
    if(opts.render) opts.render(body);
    if(opts.footer && opts.footer.length){
      var foot = document.createElement("div");
      foot.className = "drawer-foot";
      opts.footer.forEach(function(b){
        var btn = document.createElement("button");
        btn.className = "btn " + (b.cls||"");
        btn.innerHTML = (b.icon?icon(b.icon):"") + "<span></span>";
        btn.querySelector("span").textContent = b.label;
        btn.addEventListener("click", function(){ b.onClick && b.onClick(closeDrawer); });
        foot.appendChild(btn);
      });
      drawer.appendChild(foot);
    }
    backdrop.appendChild(drawer);
    root.appendChild(backdrop);
    return { close: closeDrawer, bodyEl: body, drawerEl: drawer, refreshFoot: function(newFooter){
      var oldFoot = drawer.querySelector(".drawer-foot");
      if(oldFoot) oldFoot.remove();
      if(newFooter && newFooter.length){
        var foot = document.createElement("div");
        foot.className = "drawer-foot";
        newFooter.forEach(function(b){
          var btn = document.createElement("button");
          btn.className = "btn " + (b.cls||"");
          btn.innerHTML = (b.icon?icon(b.icon):"") + "<span></span>";
          btn.querySelector("span").textContent = b.label;
          btn.addEventListener("click", function(){ b.onClick && b.onClick(closeDrawer); });
          foot.appendChild(btn);
        });
        drawer.appendChild(foot);
      }
    }};
  }

  /* ---------------- confirm dialog ---------------- */
  function confirmDialog(opts){
    return new Promise(function(resolve){
      openModal({
        title: opts.title || "Are you sure?",
        dismissable: true,
        html: '<p style="font-size:13.5px;color:var(--text-muted);line-height:1.55">'+U.escapeHtml(opts.message||"")+'</p>',
        footer: [
          { label: opts.cancelLabel || "Cancel", cls:"btn-ghost", onClick: function(close){ close(); resolve(false); } },
          { label: opts.confirmLabel || "Confirm", cls: opts.danger ? "btn-danger" : "btn-primary", onClick: function(close){ close(); resolve(true); } }
        ]
      });
    });
  }

  /* ---------------- empty state ---------------- */
  function emptyStateHtml(opts){
    return '<div class="empty-state">'+icon(opts.icon||"inbox")+
      '<div class="msg">'+U.escapeHtml(opts.msg||"Nothing here yet")+'</div>'+
      '<div class="sub">'+U.escapeHtml(opts.sub||"")+'</div></div>';
  }

  /* ---------------- form builder ---------------- */
  function renderForm(container, fields, values){
    values = values || {};
    container.innerHTML = "";
    var grid = document.createElement("div");
    grid.className = "form-grid";
    var inputs = {};
    fields.forEach(function(f){
      var wrap = document.createElement("div");
      wrap.className = "form-field" + (f.full ? " full" : "");
      if(f.type==="checkbox"){
        wrap.innerHTML = '<label class="checkbox-row"><input type="checkbox" data-k="'+f.key+'"> <span>'+U.escapeHtml(f.label)+'</span></label>';
        var cb = wrap.querySelector("input");
        cb.checked = !!values[f.key];
        inputs[f.key] = cb;
      } else {
        var labelHtml = '<label>'+U.escapeHtml(f.label)+(f.required?' <span style="color:var(--red-500)">*</span>':'')+'</label>';
        var inputHtml = "";
        if(f.type==="select"){
          inputHtml = '<select data-k="'+f.key+'">' + (f.options||[]).map(function(o){
            var val = typeof o==="object" ? o.value : o;
            var lab = typeof o==="object" ? o.label : o;
            return '<option value="'+U.escapeHtml(val)+'">'+U.escapeHtml(lab)+'</option>';
          }).join("") + '</select>';
        } else if(f.type==="textarea"){
          inputHtml = '<textarea data-k="'+f.key+'" rows="'+(f.rows||3)+'" placeholder="'+U.escapeHtml(f.placeholder||"")+'"></textarea>';
        } else {
          var itype = f.type==="currency" ? "number" : (f.type||"text");
          var step = f.type==="currency" ? ' step="0.01"' : (f.type==="number" ? ' step="1"' : "");
          inputHtml = '<input type="'+itype+'" data-k="'+f.key+'" placeholder="'+U.escapeHtml(f.placeholder||"")+'"'+step+'>';
        }
        wrap.innerHTML = labelHtml + inputHtml + (f.hint ? '<span class="hint">'+U.escapeHtml(f.hint)+'</span>' : '') + '<span class="err" data-err="'+f.key+'" hidden></span>';
        var input = wrap.querySelector("[data-k]");
        if(values[f.key]!==undefined && values[f.key]!==null) input.value = values[f.key];
        inputs[f.key] = input;
      }
      grid.appendChild(wrap);
    });
    container.appendChild(grid);

    function getValues(){
      var out = {};
      fields.forEach(function(f){
        var el = inputs[f.key];
        if(f.type==="checkbox") out[f.key] = el.checked;
        else if(f.type==="number") out[f.key] = el.value===""? null : Number(el.value);
        else if(f.type==="currency") out[f.key] = el.value===""? 0 : Math.round(Number(el.value)*100)/100;
        else out[f.key] = el.value;
      });
      return out;
    }
    function validate(){
      var ok = true;
      fields.forEach(function(f){
        var errEl = container.querySelector('[data-err="'+f.key+'"]');
        if(!errEl) return;
        var el = inputs[f.key];
        var invalid = f.required && f.type!=="checkbox" && (!el.value || String(el.value).trim()==="");
        if(invalid){ errEl.hidden=false; errEl.textContent = f.label + " is required"; ok=false; }
        else { errEl.hidden = true; }
      });
      return ok;
    }
    return { getValues: getValues, validate: validate, inputs: inputs };
  }

  /* ---------------- charts (inline SVG) ---------------- */
  var PALETTE = ["#2f6f5e","#4fa987","#2c6ea6","#b8860b","#c0392b","#6b7280","#8e6fb0"];

  function monthlyBarChart(container, series, opts){
    // series: [{label, data:[{x,y}]}]  single or dual series (revenue vs expense)
    opts = opts || {};
    var width = opts.width || container.clientWidth || 560;
    var height = opts.height || 220;
    var padL = 46, padB = 26, padT = 10, padR = 10;
    var w = width - padL - padR, h = height - padT - padB;
    var xs = series[0].data.map(function(d){return d.x;});
    var allVals = [];
    series.forEach(function(s){ s.data.forEach(function(d){ allVals.push(d.y); }); });
    var maxV = Math.max.apply(null, allVals.concat([1])) * 1.15;
    var groupW = w / xs.length;
    var barW = Math.min(18, (groupW / series.length) * 0.55);
    var gridLines = 4;
    var svg = '<svg viewBox="0 0 '+width+' '+height+'" width="100%" height="'+height+'" style="overflow:visible">';
    for(var g=0; g<=gridLines; g++){
      var gy = padT + h - (h*g/gridLines);
      var val = (maxV*g/gridLines);
      svg += '<line x1="'+padL+'" y1="'+gy+'" x2="'+(width-padR)+'" y2="'+gy+'" stroke="var(--border)" stroke-width="1"/>';
      svg += '<text x="'+(padL-8)+'" y="'+(gy+3)+'" font-size="9.5" text-anchor="end" fill="var(--text-faint)">'+Math.round(val)+'</text>';
    }
    xs.forEach(function(x, i){
      var gx = padL + i*groupW;
      series.forEach(function(s, si){
        var d = s.data[i];
        var bh = (d.y/maxV) * h;
        var bx = gx + (groupW - series.length*barW - (series.length-1)*3)/2 + si*(barW+3);
        var by = padT + h - bh;
        svg += '<rect x="'+bx.toFixed(1)+'" y="'+by.toFixed(1)+'" width="'+barW+'" height="'+bh.toFixed(1)+'" rx="2.5" fill="'+(s.color||PALETTE[si])+'"><title>'+s.label+' — '+x+': '+U.money(d.y)+'</title></rect>';
      });
      svg += '<text x="'+(gx+groupW/2)+'" y="'+(height-8)+'" font-size="10" text-anchor="middle" fill="var(--text-faint)">'+x+'</text>';
    });
    svg += '</svg>';
    container.innerHTML = svg;
    if(opts.legend!==false){
      var leg = document.createElement("div");
      leg.className = "chart-legend";
      leg.innerHTML = series.map(function(s,i){
        return '<span class="item"><span class="sw" style="background:'+(s.color||PALETTE[i])+'"></span>'+U.escapeHtml(s.label)+'</span>';
      }).join("");
      container.appendChild(leg);
    }
  }

  function sparkline(container, values, opts){
    opts = opts || {};
    var width = opts.width || 120, height = opts.height || 34;
    var maxV = Math.max.apply(null, values.concat([1]));
    var minV = Math.min.apply(null, values.concat([0]));
    var range = (maxV-minV) || 1;
    var stepX = width/(values.length-1||1);
    var pts = values.map(function(v,i){
      var x = i*stepX;
      var y = height - ((v-minV)/range)*height;
      return x.toFixed(1)+","+y.toFixed(1);
    }).join(" ");
    container.innerHTML = '<svg viewBox="0 0 '+width+' '+height+'" width="'+width+'" height="'+height+'"><polyline points="'+pts+'" fill="none" stroke="'+(opts.color||"var(--brand-500)")+'" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  }

  function donutChart(container, data, opts){
    // data: [{label,value,color}]
    opts = opts || {};
    var size = opts.size || 150, stroke = opts.stroke || 18;
    var r = (size - stroke)/2, cx=size/2, cy=size/2;
    var total = data.reduce(function(s,d){return s+d.value;},0) || 1;
    var circumference = 2*Math.PI*r;
    var offset = 0;
    var svg = '<svg viewBox="0 0 '+size+' '+size+'" width="'+size+'" height="'+size+'">';
    svg += '<circle cx="'+cx+'" cy="'+cy+'" r="'+r+'" fill="none" stroke="var(--surface-2)" stroke-width="'+stroke+'"/>';
    data.forEach(function(d,i){
      var frac = d.value/total;
      var len = frac*circumference;
      var color = d.color || PALETTE[i%PALETTE.length];
      svg += '<circle cx="'+cx+'" cy="'+cy+'" r="'+r+'" fill="none" stroke="'+color+'" stroke-width="'+stroke+'" stroke-dasharray="'+len.toFixed(1)+' '+(circumference-len).toFixed(1)+'" stroke-dashoffset="'+(-offset).toFixed(1)+'" transform="rotate(-90 '+cx+' '+cy+')"><title>'+U.escapeHtml(d.label)+': '+U.money(d.value)+'</title></circle>';
      offset += len;
    });
    svg += '</svg>';
    container.innerHTML = svg;
  }

  function barListRow(container, label, value, max, color){
    var pct = max>0 ? U.clamp((value/max)*100,0,100) : 0;
    var row = document.createElement("div");
    row.className = "bar-row";
    row.innerHTML = '<div class="label" title="'+U.escapeHtml(label)+'">'+U.escapeHtml(label)+'</div><div class="track"><div class="fill" style="width:'+pct+'%;'+(color?('background:'+color):'')+'"></div></div><div class="val">'+U.money(value)+'</div>';
    container.appendChild(row);
  }

  /* ---------------- quick single-step import mock ---------------- */
  function quickImportModal(opts){
    var fileName = null;
    var handle = openModal({
      title: opts.title || "Import file"
    });
    paint(handle.bodyEl);
    function paint(body){
      body.innerHTML =
        (opts.subtitle ? '<p class="muted" style="margin-bottom:14px">'+U.escapeHtml(opts.subtitle)+'</p>' : '') +
        '<div class="dropzone" id="qi-dropzone">'+icon("upload")+'<div style="font-weight:600;color:var(--text)">'+(fileName?U.escapeHtml(fileName):(opts.dropLabel||"Click to choose a file"))+'</div>'+
        '<div class="sub" style="font-size:11.5px;margin-top:4px">'+(opts.acceptHint||".csv, .xls, .xlsx")+'</div>'+
        '<input type="file" id="qi-file" accept="'+(opts.accept||"")+'" style="display:none"></div>'+
        '<div id="qi-progress-wrap" style="margin-top:16px;display:none"><div class="muted" style="margin-bottom:6px" id="qi-progress-label">Importing…</div><div class="progress-bar"><div class="fill" id="qi-progress-fill" style="width:0%"></div></div></div>';
      var dz = body.querySelector("#qi-dropzone");
      var fi = body.querySelector("#qi-file");
      dz.addEventListener("click", function(){ fi.click(); });
      fi.addEventListener("change", function(){
        fileName = (fi.files && fi.files[0]) ? fi.files[0].name : (opts.defaultFileName || "import_file.csv");
        paint(body);
      });
      paintFoot();
    }
    function paintFoot(){
      var modalEl = handle.modalEl;
      var old = modalEl.querySelector(".modal-foot");
      if(old) old.remove();
      var foot = document.createElement("div");
      foot.className = "modal-foot";
      var cancel = document.createElement("button");
      cancel.className = "btn btn-ghost"; cancel.textContent = "Cancel";
      cancel.addEventListener("click", closeModal);
      var go = document.createElement("button");
      go.className = "btn btn-primary"; go.textContent = opts.confirmLabel || "Import";
      go.disabled = !fileName;
      go.addEventListener("click", function(){
        if(!fileName) return;
        runProgress(modalEl);
      });
      foot.appendChild(cancel); foot.appendChild(go);
      modalEl.appendChild(foot);
    }
    function runProgress(modalEl){
      var foot = modalEl.querySelector(".modal-foot");
      if(foot) foot.style.display = "none";
      var wrap = modalEl.querySelector("#qi-progress-wrap");
      var fill = modalEl.querySelector("#qi-progress-fill");
      wrap.style.display = "block";
      var pct = 0;
      var timer = setInterval(function(){
        pct += 25;
        fill.style.width = Math.min(pct,100)+"%";
        if(pct>=100){
          clearInterval(timer);
          setTimeout(function(){
            var count = opts.onImport ? (opts.onImport(fileName)||0) : 0;
            closeModal();
            toast(count>0 ? (count+" record(s) imported from "+fileName) : ("Imported "+fileName), "success");
            if(opts.afterImport) opts.afterImport();
          }, 300);
        }
      }, 200);
    }
  }

  /* ---------------- generic list screen ---------------- */
  function renderListScreen(container, cfg){
    var state = { search:"", filters:{}, sort: cfg.defaultSort || {key:"date", dir:"desc"}, page:1 };
    (cfg.filters||[]).forEach(function(f){ state.filters[f.key] = ""; });
    var pageSize = cfg.pageSize || 8;

    var wrap = document.createElement("div");
    container.appendChild(wrap);

    // header
    var head = document.createElement("div");
    head.className = "page-head";
    head.innerHTML = '<div><div class="section-title">'+U.escapeHtml(cfg.heading||"")+'</div><p class="muted">'+U.escapeHtml(cfg.subheading||"")+'</p></div>';
    var actions = document.createElement("div");
    actions.className = "page-head-actions";
    (cfg.secondaryActions||[]).forEach(function(a){
      var b = document.createElement("button");
      b.className = "btn"; b.innerHTML = icon(a.icon||"download")+"<span></span>";
      b.querySelector("span").textContent = a.label;
      b.addEventListener("click", function(){ a.onClick(refreshAll); });
      actions.appendChild(b);
    });
    if(cfg.primaryAction){
      var pb = document.createElement("button");
      pb.className = "btn btn-primary"; pb.innerHTML = icon(cfg.primaryAction.icon||"plus")+"<span></span>";
      pb.querySelector("span").textContent = cfg.primaryAction.label;
      pb.addEventListener("click", function(){ cfg.primaryAction.onClick(refreshAll); });
      actions.appendChild(pb);
    }
    head.appendChild(actions);
    wrap.appendChild(head);

    // toolbar
    var toolbar = document.createElement("div");
    toolbar.className = "toolbar";
    var searchBox = document.createElement("div");
    searchBox.className = "search-box";
    searchBox.innerHTML = icon("search") + '<input type="search" placeholder="'+U.escapeHtml(cfg.searchPlaceholder||"Search…")+'">';
    var searchInput = searchBox.querySelector("input");
    searchInput.addEventListener("input", U.debounce(function(){ state.search = searchInput.value.trim().toLowerCase(); state.page=1; renderTable(); }, 200));
    toolbar.appendChild(searchBox);

    (cfg.filters||[]).forEach(function(f){
      var sel = document.createElement("select");
      var optsHtml = '<option value="">'+U.escapeHtml(f.label)+' (all)</option>' + f.options.map(function(o){
        return '<option value="'+U.escapeHtml(o.value)+'">'+U.escapeHtml(o.label)+'</option>';
      }).join("");
      sel.innerHTML = optsHtml;
      sel.addEventListener("change", function(){ state.filters[f.key] = sel.value; state.page=1; renderTable(); });
      toolbar.appendChild(sel);
    });
    var spacer = document.createElement("div"); spacer.className="toolbar-spacer"; toolbar.appendChild(spacer);
    wrap.appendChild(toolbar);

    var countEl = document.createElement("div");
    countEl.className = "result-count";
    wrap.appendChild(countEl);

    var tableCard = document.createElement("div");
    tableCard.className = "card";
    wrap.appendChild(tableCard);

    function getFiltered(){
      var items = cfg.data();
      if(cfg.useProjectFilter){
        var pid = DB.prefs().currentProjectId;
        if(pid && pid!=="all") items = items.filter(function(x){ return x.project_id===pid; });
      }
      if(state.search){
        var keys = cfg.searchKeys||[];
        items = items.filter(function(x){
          return keys.some(function(k){
            var v = typeof k==="function" ? k(x) : x[k];
            return v && String(v).toLowerCase().indexOf(state.search)>=0;
          });
        });
      }
      Object.keys(state.filters).forEach(function(k){
        var v = state.filters[k];
        if(v) items = items.filter(function(x){ return String(x[k])===v; });
      });
      if(state.sort && state.sort.key){
        var col = (cfg.columns||[]).find(function(c){return c.key===state.sort.key;});
        items = items.slice().sort(function(a,b){
          var av = col && col.sortValue ? col.sortValue(a) : a[state.sort.key];
          var bv = col && col.sortValue ? col.sortValue(b) : b[state.sort.key];
          if(av<bv) return state.sort.dir==="asc"?-1:1;
          if(av>bv) return state.sort.dir==="asc"?1:-1;
          return 0;
        });
      }
      return items;
    }

    function renderTable(){
      var items = getFiltered();
      countEl.textContent = items.length + (items.length===1?" result":" results");
      var totalPages = Math.max(1, Math.ceil(items.length/pageSize));
      state.page = U.clamp(state.page,1,totalPages);
      var pageItems = items.slice((state.page-1)*pageSize, state.page*pageSize);

      if(items.length===0){
        tableCard.innerHTML = emptyStateHtml({ icon: cfg.emptyIcon, msg: cfg.emptyMsg||"No results found", sub: cfg.emptySub||"Try adjusting your search or filters." });
        return;
      }

      var html = '<div class="table-wrap"><table><thead><tr>';
      (cfg.columns||[]).forEach(function(c){
        var arrow = "";
        if(state.sort.key===c.key) arrow = '<span class="sort-arrow">'+(state.sort.dir==="asc"?"▲":"▼")+'</span>';
        html += '<th class="'+(c.align==="right"?"num ":"")+(c.sortable?"sortable":"")+'" data-sort="'+(c.sortable?c.key:"")+'">'+U.escapeHtml(c.label)+arrow+'</th>';
      });
      html += '<th></th></tr></thead><tbody>';
      pageItems.forEach(function(item){
        html += '<tr class="clickable" data-id="'+item.id+'">';
        (cfg.columns||[]).forEach(function(c){
          html += '<td class="'+(c.align==="right"?"num":"")+'">'+c.render(item)+'</td>';
        });
        html += '<td><div class="row-actions">';
        if(cfg.onEdit) html += '<button class="icon-btn btn-sm" data-act="edit" title="Edit">'+icon("edit")+'</button>';
        if(cfg.onDelete) html += '<button class="icon-btn btn-sm" data-act="delete" title="Delete">'+icon("trash")+'</button>';
        html += '</div></td></tr>';
      });
      html += '</tbody></table></div>';

      html += '<div class="pagination"><span>Page '+state.page+' of '+totalPages+'</span><div class="pg-btns">';
      html += '<button data-pg="prev" '+(state.page<=1?"disabled":"")+'>'+icon("chevronLeft")+'</button>';
      html += '<button data-pg="next" '+(state.page>=totalPages?"disabled":"")+'>'+icon("chevronRight")+'</button>';
      html += '</div></div>';

      tableCard.innerHTML = html;

      U.qsa("thead th[data-sort]", tableCard).forEach(function(th){
        th.addEventListener("click", function(){
          var k = th.getAttribute("data-sort");
          if(!k) return;
          if(state.sort.key===k) state.sort.dir = state.sort.dir==="asc"?"desc":"asc";
          else state.sort = {key:k, dir:"asc"};
          renderTable();
        });
      });
      U.qsa("tbody tr", tableCard).forEach(function(tr){
        tr.addEventListener("click", function(e){
          if(e.target.closest("[data-act]")) return;
          var item = cfg.data().find(function(x){return x.id===tr.getAttribute("data-id");});
          if(item && cfg.onRowClick) cfg.onRowClick(item, refreshAll);
        });
      });
      U.qsa('[data-act="edit"]', tableCard).forEach(function(b){
        b.addEventListener("click", function(e){
          e.stopPropagation();
          var id = e.target.closest("tr").getAttribute("data-id");
          var item = cfg.data().find(function(x){return x.id===id;});
          cfg.onEdit(item, refreshAll);
        });
      });
      U.qsa('[data-act="delete"]', tableCard).forEach(function(b){
        b.addEventListener("click", function(e){
          e.stopPropagation();
          var id = e.target.closest("tr").getAttribute("data-id");
          var item = cfg.data().find(function(x){return x.id===id;});
          cfg.onDelete(item, refreshAll);
        });
      });
      var prevBtn = tableCard.querySelector('[data-pg="prev"]');
      var nextBtn = tableCard.querySelector('[data-pg="next"]');
      if(prevBtn) prevBtn.addEventListener("click", function(){ state.page--; renderTable(); });
      if(nextBtn) nextBtn.addEventListener("click", function(){ state.page++; renderTable(); });
    }

    function refreshAll(){ renderTable(); }
    renderTable();
    return { refresh: refreshAll };
  }

  window.Contapop.UI = {
    icon: icon, statusBadge: statusBadge, badge: badge, toast: toast,
    openModal: openModal, closeModal: closeModal,
    openDrawer: openDrawer, closeDrawer: closeDrawer,
    confirmDialog: confirmDialog, emptyStateHtml: emptyStateHtml,
    renderForm: renderForm, renderListScreen: renderListScreen, quickImportModal: quickImportModal,
    monthlyBarChart: monthlyBarChart, sparkline: sparkline, donutChart: donutChart, barListRow: barListRow,
    PALETTE: PALETTE
  };
})();
