/* ==========================================================================
   Contapop prototype — shell bootstrap (sidebar, topbar, panels)
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils;
  var UI = window.Contapop.UI;
  var DB = window.Contapop.DB;
  var ST = window.Contapop.state;

  function buildSidebarNav(){
    var nav = U.byId("sidebar-nav");
    var html = "";
    ST.NAV_ITEMS.forEach(function(item){
      if(item.section){
        html += '<div class="nav-section-label">'+U.escapeHtml(item.section)+'</div>';
      } else {
        html += '<button class="nav-item" data-route="'+item.route+'">'+UI.icon(item.icon)+'<span>'+U.escapeHtml(item.label)+'</span></button>';
      }
    });
    nav.innerHTML = html;
    U.qsa(".nav-item", nav).forEach(function(btn){
      btn.addEventListener("click", function(){ window.Contapop.router.navigate(btn.getAttribute("data-route")); });
    });
  }

  function buildTenantFooter(){
    var t = DB.state.tenant;
    var user = DB.currentUser();
    var el = U.byId("sidebar-tenant");
    el.innerHTML = '<div class="tenant-mark">'+U.escapeHtml((t.name||"?").slice(0,1))+'</div>'+
      '<div class="tenant-meta"><div class="name"></div><div class="role"></div></div>';
    el.querySelector(".name").textContent = t.name;
    el.querySelector(".role").textContent = user.role + " · " + t.plan + " plan";
  }

  function buildProjectSelect(){
    var projects = DB.all("projects");
    var opts = '<option value="all">All Projects</option>' + projects.map(function(p){
      return '<option value="'+p.id+'">'+U.escapeHtml(p.name)+'</option>';
    }).join("");
    var selects = [U.byId("project-select"), U.byId("project-select-mobile")];
    selects.forEach(function(sel){
      sel.innerHTML = opts;
      sel.value = DB.prefs().currentProjectId || "all";
      sel.addEventListener("change", function(){
        DB.setPref("currentProjectId", sel.value);
        selects.forEach(function(other){ if(other!==sel) other.value = sel.value; });
        window.Contapop.router.renderRoute();
      });
    });
  }

  /* ---------------- notifications panel ---------------- */
  function refreshNotifDot(){
    var unread = DB.all("notifications").filter(function(n){return !n.read;}).length;
    U.byId("notif-dot").hidden = unread===0;
  }
  function renderNotifPanel(){
    var panel = U.byId("notif-panel");
    var items = DB.all("notifications");
    var kindIcon = { warning:"alert", success:"checkCircle", info:"info" };
    var html = '<div class="panel-header"><span>Notifications</span><button id="notif-mark-all">Mark all read</button></div>';
    if(items.length===0){
      html += UI.emptyStateHtml({icon:"bell", msg:"You're all caught up", sub:""});
    } else {
      items.forEach(function(n){
        html += '<div class="notif-item '+(n.read?"read":"")+'"><span class="dot"></span><div><div class="t">'+U.escapeHtml(n.title)+'</div><div>'+U.escapeHtml(n.body)+'</div><div class="muted" style="margin-top:3px">'+U.formatDateShort(n.date)+'</div></div></div>';
      });
    }
    panel.innerHTML = html;
    var markAll = U.byId("notif-mark-all");
    if(markAll) markAll.addEventListener("click", function(){
      DB.all("notifications").forEach(function(n){ n.read = true; });
      DB.save();
      refreshNotifDot();
      renderNotifPanel();
    });
  }

  function renderUserPanel(){
    var panel = U.byId("user-panel");
    var user = DB.currentUser();
    var html = '<div class="user-panel-head"><div class="name"></div><div class="email"></div></div><div class="user-panel-divider"></div>';
    html += '<div class="user-panel-item" data-nav="user">'+UI.icon("user")+' My Profile</div>';
    html += '<div class="user-panel-item" data-nav="settings">'+UI.icon("settings")+' Settings</div>';
    html += '<div class="user-panel-divider"></div>';
    html += '<div class="user-panel-item" id="theme-toggle-item">'+UI.icon(DB.prefs().theme==="dark"?"sun":"moon")+' <span>'+(DB.prefs().theme==="dark"?"Light mode":"Dark mode")+'</span></div>';
    html += '<div class="user-panel-item" id="logout-item">'+UI.icon("logout")+' Sign out</div>';
    panel.innerHTML = html;
    panel.querySelector(".name").textContent = user.name;
    panel.querySelector(".email").textContent = user.email;
    U.qsa("[data-nav]", panel).forEach(function(elm){
      elm.addEventListener("click", function(){
        closeAllPanels();
        window.Contapop.router.navigate(elm.getAttribute("data-nav"));
      });
    });
    U.byId("theme-toggle-item").addEventListener("click", function(){
      var next = DB.prefs().theme==="dark" ? "light" : "dark";
      DB.setPref("theme", next);
      ST.applyTheme(next);
      renderUserPanel();
    });
    U.byId("logout-item").addEventListener("click", function(){
      closeAllPanels();
      UI.toast("Signed out (demo only — no auth in this prototype)", "info");
    });
  }

  function closeAllPanels(){
    U.byId("notif-panel").hidden = true;
    U.byId("user-panel").hidden = true;
  }

  function wireTopbar(){
    var avatarBtn = U.byId("user-menu-btn");
    avatarBtn.textContent = DB.currentUser().initials;

    U.byId("notif-btn").addEventListener("click", function(e){
      e.stopPropagation();
      var panel = U.byId("notif-panel");
      var willOpen = panel.hidden;
      closeAllPanels();
      if(willOpen){ renderNotifPanel(); panel.hidden = false; }
    });
    avatarBtn.addEventListener("click", function(e){
      e.stopPropagation();
      var panel = U.byId("user-panel");
      var willOpen = panel.hidden;
      closeAllPanels();
      if(willOpen){ renderUserPanel(); panel.hidden = false; }
    });
    document.addEventListener("click", function(e){
      if(!e.target.closest(".notif-panel,.user-panel,#notif-btn,.user-menu")) closeAllPanels();
    });
    refreshNotifDot();

    // global search — lightweight cross-entity search dropdown
    var searchInput = U.byId("global-search");
    var searchPanel = document.createElement("div");
    searchPanel.className = "notif-panel";
    searchPanel.style.left = "auto";
    searchPanel.hidden = true;
    document.body.appendChild(searchPanel);

    function positionSearchPanel(){
      var r = searchInput.getBoundingClientRect();
      searchPanel.style.top = (r.bottom + 6) + "px";
      searchPanel.style.left = r.left + "px";
      searchPanel.style.right = "auto";
      searchPanel.style.width = Math.max(280, r.width) + "px";
    }

    function runGlobalSearch(q){
      q = q.trim().toLowerCase();
      if(!q){ searchPanel.hidden = true; return; }
      var results = [];
      DB.all("invoices").forEach(function(i){ if((i.number+" "+i.client).toLowerCase().indexOf(q)>=0) results.push({label:i.number+" — "+i.client, sub:"Invoice · "+U.money(i.amount), route:"invoices"}); });
      DB.all("expenses").forEach(function(x){ if(x.description.toLowerCase().indexOf(q)>=0) results.push({label:x.description, sub:"Expense · "+U.money(x.amount), route:"expenses"}); });
      DB.all("revenues").forEach(function(x){ if(x.description.toLowerCase().indexOf(q)>=0) results.push({label:x.description, sub:"Revenue · "+U.money(x.amount), route:"revenues"}); });
      DB.all("projects").forEach(function(p){ if(p.name.toLowerCase().indexOf(q)>=0) results.push({label:p.name, sub:"Project", route:"overview"}); });
      results = results.slice(0,7);
      var html = '<div class="panel-header"><span>Search results</span></div>';
      if(results.length===0){ html += UI.emptyStateHtml({icon:"search", msg:"No matches", sub:"Try a different term"}); }
      else results.forEach(function(r,i){
        html += '<div class="user-panel-item" data-i="'+i+'"><div><div style="font-weight:600">'+U.escapeHtml(r.label)+'</div><div class="muted">'+r.sub+'</div></div></div>';
      });
      searchPanel.innerHTML = html;
      U.qsa("[data-i]", searchPanel).forEach(function(elm){
        elm.addEventListener("click", function(){
          var r = results[Number(elm.getAttribute("data-i"))];
          searchPanel.hidden = true;
          searchInput.value = "";
          window.Contapop.router.navigate(r.route);
        });
      });
      positionSearchPanel();
      searchPanel.hidden = false;
    }
    searchInput.addEventListener("input", U.debounce(function(){ runGlobalSearch(searchInput.value); }, 200));
    searchInput.addEventListener("focus", function(){ if(searchInput.value) runGlobalSearch(searchInput.value); });
    document.addEventListener("click", function(e){ if(!e.target.closest("#global-search") && e.target!==searchPanel) searchPanel.hidden = true; });

    // mobile nav toggle
    U.byId("menu-toggle").addEventListener("click", function(){
      document.getElementById("app").classList.toggle("nav-open");
    });
    U.byId("sidebar-overlay").addEventListener("click", function(){
      document.getElementById("app").classList.remove("nav-open");
    });
  }

  function init(){
    buildSidebarNav();
    buildTenantFooter();
    buildProjectSelect();
    wireTopbar();
    window.Contapop.router.renderRoute();
  }

  if(document.readyState==="loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
