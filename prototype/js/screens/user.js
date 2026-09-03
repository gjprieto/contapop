/* ==========================================================================
   User / My Profile screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB;

  function render(container){
    var user = DB.currentUser();
    var prefs = DB.prefs();

    var stats = {
      expenses: DB.all("expenses").length,
      revenues: DB.all("revenues").length,
      invoices: DB.all("invoices").length,
      reports: DB.all("reports").length
    };

    container.innerHTML =
      '<div class="grid grid-2" style="align-items:start;gap:16px">' +
        '<div class="card card-pad">' +
          '<div class="flex gap-8" style="align-items:center;margin-bottom:18px">' +
            '<div class="avatar-btn" style="width:52px;height:52px;font-size:18px;cursor:default">'+user.initials+'</div>' +
            '<div><div style="font-weight:700;font-size:16px">'+U.escapeHtml(user.name)+'</div><div class="muted">'+U.escapeHtml(user.role)+' · '+U.escapeHtml(DB.state.tenant.name)+'</div></div>' +
          '</div>' +
          '<div id="profile-form"></div>' +
          '<div style="margin-top:16px"><button class="btn btn-primary" id="profile-save">Save profile</button></div>' +
        '</div>' +
        '<div style="display:flex;flex-direction:column;gap:16px">' +
          '<div class="card card-pad">' +
            '<div class="section-title" style="margin-bottom:12px">Usage this workspace</div>' +
            '<div class="grid grid-2" style="gap:10px">' +
              usageStat("Expenses logged", stats.expenses, "expenses") +
              usageStat("Revenues logged", stats.revenues, "revenues") +
              usageStat("Invoices created", stats.invoices, "invoices") +
              usageStat("Reports generated", stats.reports, "reports") +
            '</div>' +
          '</div>' +
          '<div class="card card-pad">' +
            '<div class="section-title" style="margin-bottom:6px">Notification preferences</div>' +
            '<p class="muted" style="margin-bottom:10px">Manage what you get notified about.</p>' +
            toggleRow("notifyEmail","Email notifications", prefs.notifyEmail) +
            toggleRow("notifyOverdue","Overdue invoice alerts", prefs.notifyOverdue) +
            toggleRow("notifyBudget","Budget alerts", prefs.notifyBudget) +
          '</div>' +
        '</div>' +
      '</div>' +
      '<div class="card" style="margin-top:16px">' +
        '<div class="card-head"><h3>Your recent activity</h3></div>' +
        '<div class="card-body" id="profile-activity"></div>' +
      '</div>';

    var form = UI.renderForm(U.byId("profile-form"), [
      { key:"name", label:"Full name", required:true },
      { key:"email", label:"Email address", required:true },
      { key:"phone", label:"Phone number" }
    ], user);
    U.byId("profile-save").addEventListener("click", function(){
      if(!form.validate()) return;
      Object.assign(user, form.getValues());
      DB.save();
      UI.toast("Profile saved", "success");
      window.Contapop.router.renderRoute();
    });

    U.qsa("[data-toggle]", container).forEach(function(cb){
      cb.addEventListener("change", function(){
        DB.setPref(cb.getAttribute("data-toggle"), cb.checked);
        UI.toast("Preference saved", "success");
      });
    });

    var activity = DB.all("activity");
    U.byId("profile-activity").innerHTML = '<div class="timeline">' + activity.map(function(a){
      return '<div class="timeline-item"><span class="timeline-dot"></span><div><div class="t">'+U.escapeHtml(a.text)+'</div><div class="d">'+U.formatDate(a.date)+'</div></div></div>';
    }).join("") + '</div>';

    function usageStat(label, value, icon){
      return '<div class="card-pad" style="background:var(--surface-2);border-radius:8px;padding:12px"><div class="stat-icon icon-bg-brand" style="width:26px;height:26px;margin-bottom:8px">'+UI.icon(icon)+'</div><div style="font-size:18px;font-weight:700">'+value+'</div><div class="muted">'+label+'</div></div>';
    }
    function toggleRow(key,label,checked){
      return '<div class="settings-row"><div class="lbl" style="font-size:12.5px">'+label+'</div>' +
        '<label class="toggle"><input type="checkbox" data-toggle="'+key+'" '+(checked?"checked":"")+'><span class="track"></span><span class="thumb"></span></label></div>';
    }
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.user = {
    title:"My Profile",
    subtitle:"Your personal information and activity",
    render: render
  };
})();
