/* ==========================================================================
   Contapop prototype — utilities
   ========================================================================== */
(function(){
  "use strict";

  var currencyFmt = new Intl.NumberFormat("en-US", { style:"currency", currency:"EUR" });
  var numberFmt = new Intl.NumberFormat("en-US");

  function money(n){
    n = Number(n)||0;
    return currencyFmt.format(n);
  }
  function moneySigned(n){
    n = Number(n)||0;
    var s = currencyFmt.format(Math.abs(n));
    return (n<0? "-":"+") + s;
  }
  function num(n){ return numberFmt.format(Number(n)||0); }

  function formatDate(iso, opts){
    if(!iso) return "—";
    var d = new Date(iso+"T00:00:00");
    if(isNaN(d.getTime())) return iso;
    opts = opts || { month:"short", day:"numeric", year:"numeric" };
    return d.toLocaleDateString("en-US", opts);
  }
  function formatDateShort(iso){ return formatDate(iso, { month:"short", day:"numeric" }); }
  function formatMonthYear(iso){ return formatDate(iso, { month:"long", year:"numeric" }); }
  function relativeDay(iso){
    var today = new Date(window.Contapop.helpers.today()+"T00:00:00");
    var d = new Date(iso+"T00:00:00");
    var diff = Math.round((d-today)/86400000);
    if(diff===0) return "Today";
    if(diff===-1) return "Yesterday";
    if(diff===1) return "Tomorrow";
    if(diff<0) return Math.abs(diff)+"d ago";
    return "in "+diff+"d";
  }

  function escapeHtml(str){
    if(str===null||str===undefined) return "";
    return String(str).replace(/[&<>"']/g, function(c){
      return {"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c];
    });
  }

  function debounce(fn, wait){
    var t;
    return function(){
      var args = arguments, ctx = this;
      clearTimeout(t);
      t = setTimeout(function(){ fn.apply(ctx,args); }, wait||250);
    };
  }

  function classNames(){
    return Array.prototype.slice.call(arguments).filter(Boolean).join(" ");
  }

  function titleCase(s){
    if(!s) return "";
    return String(s).replace(/_/g," ").replace(/\b\w/g, function(c){return c.toUpperCase();});
  }

  function uidLocal(prefix){
    return prefix + "_" + Math.random().toString(36).slice(2,9);
  }

  function byId(id){ return document.getElementById(id); }

  function qs(sel,root){ return (root||document).querySelector(sel); }
  function qsa(sel,root){ return Array.prototype.slice.call((root||document).querySelectorAll(sel)); }

  function sumBy(arr, fn){ return arr.reduce(function(s,x){ return s + (Number(fn(x))||0); }, 0); }

  function groupBy(arr, fn){
    var out = {};
    arr.forEach(function(x){
      var k = fn(x);
      (out[k] = out[k]||[]).push(x);
    });
    return out;
  }

  function monthKey(iso){ return iso.slice(0,7); } // YYYY-MM

  function clamp(n,min,max){ return Math.max(min, Math.min(max, n)); }

  window.Contapop = window.Contapop || {};
  window.Contapop.utils = {
    money: money, moneySigned: moneySigned, num: num,
    formatDate: formatDate, formatDateShort: formatDateShort, formatMonthYear: formatMonthYear, relativeDay: relativeDay,
    escapeHtml: escapeHtml, debounce: debounce, classNames: classNames, titleCase: titleCase,
    uid: uidLocal, byId: byId, qs: qs, qsa: qsa, sumBy: sumBy, groupBy: groupBy, monthKey: monthKey, clamp: clamp
  };
})();
