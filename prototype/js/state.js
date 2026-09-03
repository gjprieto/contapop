/* ==========================================================================
   Contapop prototype — app-wide state helpers, nav config, theming
   ========================================================================== */
(function(){
  "use strict";
  var DB = window.Contapop.DB;

  var NAV_ITEMS = [
    { route:"home", label:"Home", icon:"home" },
    { route:"overview", label:"Financial Overview", icon:"overview" },
    { section:"Money" },
    { route:"expenses", label:"Expenses", icon:"expenses" },
    { route:"revenues", label:"Revenues", icon:"revenues" },
    { route:"invoices", label:"Invoices", icon:"invoices" },
    { route:"payments", label:"Payments", icon:"payments" },
    { route:"transactions", label:"Transactions", icon:"transactions" },
    { section:"Plan & analyze" },
    { route:"reports", label:"Reports", icon:"reports" },
    { route:"plans", label:"Plans", icon:"plans" },
    { section:"Account" },
    { route:"settings", label:"Settings", icon:"settings" },
    { route:"user", label:"My Profile", icon:"user" }
  ];

  function applyTheme(theme){
    document.documentElement.setAttribute("data-theme", theme==="dark" ? "dark" : "light");
  }
  applyTheme(DB.prefs().theme);

  window.Contapop.state = {
    NAV_ITEMS: NAV_ITEMS,
    applyTheme: applyTheme
  };
})();
