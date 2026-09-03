/* ==========================================================================
   Contapop prototype — hash router
   ========================================================================== */
(function(){
  "use strict";
  var U = window.Contapop.utils;

  function currentRoute(){
    var h = location.hash.replace(/^#\/?/, "");
    return h || "home";
  }

  function setActiveNav(route){
    U.qsa(".nav-item").forEach(function(a){
      a.classList.toggle("active", a.getAttribute("data-route")===route);
    });
  }

  function renderRoute(){
    if(window.Contapop.UI){ window.Contapop.UI.closeModal(); window.Contapop.UI.closeDrawer(); }
    var route = currentRoute();
    var screens = window.Contapop.screens || {};
    var screen = screens[route] || screens.home;
    if(!screen){ return; }
    U.byId("page-title").textContent = screen.title || U.titleCase(route);
    U.byId("page-subtitle").textContent = screen.subtitle || "";
    var content = U.byId("content");
    content.innerHTML = "";
    content.scrollTop = 0;
    try{
      screen.render(content);
    }catch(err){
      console.error("Screen render error", route, err);
      content.innerHTML = '<div class="empty-state">'+window.Contapop.UI.icon("alert")+'<div class="msg">Something went wrong rendering this screen.</div><div class="sub">'+U.escapeHtml(err.message||"")+'</div></div>';
    }
    setActiveNav(route);
    window.scrollTo(0,0);
    document.title = (screen.title || U.titleCase(route)) + " · Contapop";
    // close mobile nav on navigation
    document.getElementById("app").classList.remove("nav-open");
  }

  function navigate(route){
    if(currentRoute()===route){ renderRoute(); return; }
    location.hash = "#/" + route;
  }

  window.addEventListener("hashchange", renderRoute);

  window.Contapop.router = { navigate: navigate, renderRoute: renderRoute, currentRoute: currentRoute };
})();
