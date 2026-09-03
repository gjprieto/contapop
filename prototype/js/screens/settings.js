/* ==========================================================================
   Settings screen
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils, UI = window.Contapop.UI, DB = window.Contapop.DB, ST = window.Contapop.state;

  var LOGIN_ACTIVITY = [
    { device:"Chrome on Windows", location:"Madrid, Spain", date:"2026-09-03 09:12" },
    { device:"Contapop iOS App", location:"Madrid, Spain", date:"2026-09-01 18:40" },
    { device:"Chrome on Windows", location:"Madrid, Spain", date:"2026-08-29 08:55" },
    { device:"Firefox on macOS", location:"Barcelona, Spain", date:"2026-08-22 14:03" }
  ];

  function render(container){
    var tab = "account";
    var wrap = document.createElement("div");
    container.appendChild(wrap);

    function paint(){
      wrap.innerHTML =
        '<div class="tabs">' +
          tabBtn("account","Account") + tabBtn("preferences","Preferences") + tabBtn("security","Security") + tabBtn("support","Support") +
        '</div>' +
        '<div id="settings-panel"></div>';
      U.qsa(".tab-btn", wrap).forEach(function(b){
        b.addEventListener("click", function(){ tab = b.getAttribute("data-tab"); paint(); });
      });
      var panel = U.byId("settings-panel");
      if(tab==="account") renderAccount(panel);
      else if(tab==="preferences") renderPreferences(panel);
      else if(tab==="security") renderSecurity(panel);
      else renderSupport(panel);
    }
    function tabBtn(key,label){ return '<button class="tab-btn '+(tab===key?"active":"")+'" data-tab="'+key+'">'+label+'</button>'; }

    function renderAccount(panel){
      var user = DB.currentUser();
      panel.innerHTML = '<div class="card card-pad" style="max-width:560px">' +
        '<div class="section-title" style="margin-bottom:14px">Account information</div>' +
        '<div id="acct-form"></div>' +
        '<div style="margin-top:16px"><button class="btn btn-primary" id="acct-save">Save changes</button></div>' +
      '</div>' +
      '<div class="card card-pad" style="max-width:560px;margin-top:16px">' +
        '<div class="section-title" style="margin-bottom:4px">Team members</div>' +
        '<p class="muted" style="margin-bottom:14px">People with access to this tenant.</p>' +
        '<div id="team-list"></div>' +
      '</div>';
      var form = UI.renderForm(U.byId("acct-form"), [
        { key:"name", label:"Full name", required:true },
        { key:"email", label:"Email address", required:true },
        { key:"phone", label:"Phone number" },
        { key:"role", label:"Role (read-only)" }
      ], user);
      form.inputs.role.disabled = true;
      U.byId("acct-save").addEventListener("click", function(){
        if(!form.validate()) return;
        var v = form.getValues();
        Object.assign(user, { name:v.name, email:v.email, phone:v.phone });
        DB.save();
        UI.toast("Account information saved", "success");
        window.Contapop.router.renderRoute();
      });
      var teamEl = U.byId("team-list");
      teamEl.innerHTML = DB.all("users").map(function(u){
        return '<div class="flex-between" style="padding:9px 0;border-bottom:1px solid var(--border)">'+
          '<div class="flex gap-8" style="align-items:center"><span class="avatar-sm">'+u.initials+'</span><div><div style="font-weight:600;font-size:13px">'+U.escapeHtml(u.name)+'</div><div class="muted">'+U.escapeHtml(u.email)+'</div></div></div>'+
          UI.badge(u.role,"gray") + '</div>';
      }).join("");
    }

    function renderPreferences(panel){
      var prefs = DB.prefs();
      panel.innerHTML = '<div class="card card-pad" style="max-width:620px">' +
        '<div class="section-title" style="margin-bottom:14px">Appearance</div>' +
        '<div class="settings-row"><div><div class="lbl">Theme</div><div class="desc">Choose how Contapop looks on this device.</div></div>' +
          '<div class="theme-swatches">' +
            themeSwatch("light") + themeSwatch("dark") +
          '</div></div>' +
        '<div class="divider"></div>' +
        '<div class="section-title" style="margin-bottom:6px">Notifications</div>' +
        toggleRow("notifyEmail","Email notifications","Receive a daily digest by email.", prefs.notifyEmail) +
        toggleRow("notifyOverdue","Overdue invoice alerts","Get notified when an invoice becomes overdue.", prefs.notifyOverdue) +
        toggleRow("notifyBudget","Budget alerts","Get notified when a budget crosses 80% usage.", prefs.notifyBudget) +
      '</div>';
      U.qsa("[data-theme-pick]", panel).forEach(function(b){
        b.addEventListener("click", function(){
          var t = b.getAttribute("data-theme-pick");
          DB.setPref("theme", t);
          ST.applyTheme(t);
          renderPreferences(panel);
        });
      });
      U.qsa("[data-toggle]", panel).forEach(function(cb){
        cb.addEventListener("change", function(){
          DB.setPref(cb.getAttribute("data-toggle"), cb.checked);
          UI.toast("Preference saved", "success");
        });
      });
    }
    function themeSwatch(t){
      var active = DB.prefs().theme===t;
      var bg = t==="dark" ? "#1b1f22" : "#ffffff";
      var side = t==="dark" ? "#14171a" : "#f5f6f5";
      return '<button class="theme-swatch '+(active?"active":"")+'" data-theme-pick="'+t+'" title="'+U.titleCase(t)+'">'+
        '<div class="sw-top" style="background:var(--brand-500)"></div>'+
        '<div class="sw-body"><div class="sw-side" style="background:'+side+'"></div><div style="flex:1;background:'+bg+'"></div></div>'+
      '</button>';
    }
    function toggleRow(key,label,desc,checked){
      return '<div class="settings-row"><div><div class="lbl">'+label+'</div><div class="desc">'+desc+'</div></div>' +
        '<label class="toggle"><input type="checkbox" data-toggle="'+key+'" '+(checked?"checked":"")+'><span class="track"></span><span class="thumb"></span></label></div>';
    }

    function renderSecurity(panel){
      var prefs = DB.prefs();
      panel.innerHTML = '<div class="card card-pad" style="max-width:620px">' +
        '<div class="section-title" style="margin-bottom:6px">Security</div>' +
        toggleRow("twoFactor","Two-factor authentication","Require a verification code at sign in.", prefs.twoFactor) +
        '<div class="settings-row"><div><div class="lbl">Password</div><div class="desc">Last changed 3 months ago.</div></div>' +
          '<button class="btn btn-sm" id="btn-change-pw">Change password</button></div>' +
      '</div>' +
      '<div class="card card-pad" style="max-width:620px;margin-top:16px">' +
        '<div class="section-title" style="margin-bottom:4px">Recent login activity</div>' +
        '<p class="muted" style="margin-bottom:12px">Sessions on your account over the last two weeks.</p>' +
        '<div class="table-wrap"><table><thead><tr><th>Device</th><th>Location</th><th>Date</th></tr></thead><tbody>' +
          LOGIN_ACTIVITY.map(function(l){ return '<tr><td>'+l.device+'</td><td>'+l.location+'</td><td>'+l.date+'</td></tr>'; }).join("") +
        '</tbody></table></div>' +
      '</div>';
      U.qsa("[data-toggle]", panel).forEach(function(cb){
        cb.addEventListener("change", function(){
          DB.setPref(cb.getAttribute("data-toggle"), cb.checked);
          UI.toast(cb.checked ? "Two-factor authentication enabled" : "Two-factor authentication disabled", "success");
        });
      });
      U.byId("btn-change-pw").addEventListener("click", function(){
        UI.openModal({
          title:"Change password",
          html:'<div class="form-grid"><div class="form-field full"><label>Current password</label><input type="password"></div><div class="form-field full"><label>New password</label><input type="password"></div><div class="form-field full"><label>Confirm new password</label><input type="password"></div></div>',
          footer:[
            { label:"Cancel", cls:"btn-ghost", onClick:function(close){close();} },
            { label:"Update password", cls:"btn-primary", onClick:function(close){ close(); UI.toast("Password updated (demo only)", "success"); } }
          ]
        });
      });
    }

    function renderSupport(panel){
      panel.innerHTML = '<div class="grid grid-2">' +
        supportCard("info","Help Center","Browse guides and answers to common questions.","Visit Help Center") +
        supportCard("fileText","Contact support","Reach our team for account or billing questions.","Contact support") +
        supportCard("checkCircle","Send feedback","Tell us what's working and what could be better.","Send feedback") +
        supportCard("alert","Reset demo data","Restore this prototype to its original sample data.","Reset demo data", true) +
      '</div>';
      U.qsa("[data-support]", panel).forEach(function(b){
        b.addEventListener("click", function(){
          var kind = b.getAttribute("data-support");
          if(kind==="reset"){
            UI.confirmDialog({ title:"Reset demo data?", message:"This restores all sample expenses, revenues, invoices and settings to their original state. Your changes will be lost.", confirmLabel:"Reset", danger:true })
              .then(function(ok){ if(ok){ DB.reset(); ST.applyTheme(DB.prefs().theme); UI.toast("Demo data reset", "success"); window.Contapop.router.renderRoute(); } });
          } else {
            UI.toast("This is a prototype — no live support channel is connected.", "info");
          }
        });
      });
    }
    function supportCard(icon,title,desc,cta,isReset){
      return '<div class="card card-pad"><span class="stat-icon icon-bg-brand" style="margin-bottom:10px">'+UI.icon(icon)+'</span>' +
        '<div style="font-weight:700;font-size:13.5px;margin-bottom:4px">'+title+'</div>' +
        '<p class="muted" style="margin-bottom:12px">'+desc+'</p>' +
        '<button class="btn btn-sm '+(isReset?"btn-danger":"")+'" data-support="'+(isReset?"reset":"other")+'">'+cta+'</button></div>';
    }

    paint();
  }

  window.Contapop.screens = window.Contapop.screens || {};
  window.Contapop.screens.settings = {
    title:"Settings",
    subtitle:"Manage your account, preferences and security",
    render: render
  };
})();
