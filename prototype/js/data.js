/* ==========================================================================
   Contapop prototype — mock data layer
   Deterministic seeded generator + localStorage-backed "DB" with CRUD.
   ========================================================================== */

(function(){
  "use strict";

  var DB_KEY = "contapop_db_v3";
  var SCHEMA_VERSION = 3;

  /* ---------------- seeded RNG ---------------- */
  function mulberry32(a){
    return function(){
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  var rng = mulberry32(20260903);
  function rnd(){ return rng(); }
  function pick(arr){ return arr[Math.floor(rnd()*arr.length)]; }
  function pickWeighted(pairs){ // [[value,weight],...]
    var total = pairs.reduce(function(s,p){return s+p[1];},0);
    var r = rnd()*total;
    for(var i=0;i<pairs.length;i++){ r -= pairs[i][1]; if(r<=0) return pairs[i][0]; }
    return pairs[pairs.length-1][0];
  }
  function randInt(min,max){ return Math.floor(rnd()*(max-min+1))+min; }
  function randMoney(min,max){ return Math.round((rnd()*(max-min)+min)*100)/100; }
  function uid(prefix){
    uid._c = uid._c || {};
    uid._c[prefix] = (uid._c[prefix]||0)+1;
    return prefix + "_" + String(uid._c[prefix]).padStart(4,"0");
  }
  function isoDate(y,m,d){
    var mm = String(m).padStart(2,"0"), dd = String(d).padStart(2,"0");
    return y+"-"+mm+"-"+dd;
  }
  function addDays(iso,n){
    var d = new Date(iso+"T00:00:00");
    d.setDate(d.getDate()+n);
    return d.toISOString().slice(0,10);
  }
  function today(){ return "2026-09-03"; }

  /* ---------------- reference data ---------------- */
  var TENANT = { id:"t1", name:"Alex Rivera Freelance Studio", plan:"Pro", created_at:"2024-01-14" };
  var CURRENT_USER = { id:"u1", tenant_id:"t1", name:"Alex Rivera", email:"admin@alicazum.com", role:"Owner", initials:"AR", created_at:"2024-01-14", phone:"+34 611 220 934", locale:"en-US", avatarColor:"#2f6f5e" };
  var TEAM_USERS = [
    CURRENT_USER,
    { id:"u2", tenant_id:"t1", name:"Marta Solis", email:"marta@alicazum.com", role:"Accountant", initials:"MS", created_at:"2024-03-02" },
    { id:"u3", tenant_id:"t1", name:"Ben Ochoa", email:"ben@alicazum.com", role:"Viewer", initials:"BO", created_at:"2025-06-19" }
  ];

  var PROJECTS = [
    { id:"p1", tenant_id:"t1", name:"Website Redesign — Acme Retail", client:"Acme Retail", status:"active", created_at:"2026-01-10" },
    { id:"p2", tenant_id:"t1", name:"Brand Identity — Luma Wellness", client:"Luma Wellness", status:"active", created_at:"2026-02-03" },
    { id:"p3", tenant_id:"t1", name:"Retainer — Nord Consulting", client:"Nord Consulting", status:"active", created_at:"2025-11-20" },
    { id:"p4", tenant_id:"t1", name:"Mobile App — Fenwick Logistics", client:"Fenwick Logistics", status:"completed", created_at:"2025-09-01" }
  ];

  var BANK_ACCOUNTS = [
    { id:"acc1", tenant_id:"t1", project_id:null, name:"Business Checking", bank_name:"BBVA", account_number:"ES12 **** **** **** 4471", currency:"EUR", balance:18420.55 },
    { id:"acc2", tenant_id:"t1", project_id:null, name:"Tax Reserve Savings", bank_name:"CaixaBank", account_number:"ES77 **** **** **** 8820", currency:"EUR", balance:6120.00 }
  ];

  var CARDS = [
    { id:"card1", tenant_id:"t1", project_id:null, cardholder_name:"Alex Rivera", card_number:"**** **** **** 2214", brand:"Visa Debit", expiration_date:"2028-08-31" },
    { id:"card2", tenant_id:"t1", project_id:null, cardholder_name:"Alex Rivera", card_number:"**** **** **** 7735", brand:"Mastercard Corporate", expiration_date:"2027-03-31" }
  ];

  var EXPENSE_CATEGORIES = ["Software & Tools","Office & Supplies","Travel","Marketing","Professional Services","Taxes & Fees","Equipment","Utilities","Subcontractors"];
  var REVENUE_CATEGORIES = ["Web Development","Design Services","Consulting","Retainer","Licensing"];

  var MONTHS_2026 = [1,2,3,4,5,6,7,8,9]; // Jan..Sep (current month Sep, partial)
  var MONTH_NAMES = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
  var DAYS_IN_MONTH = {1:31,2:28,3:31,4:30,5:31,6:30,7:31,8:31,9:3};

  function projectForIndex(i){ return PROJECTS[i % PROJECTS.length].id; }

  /* ---------------- generators ---------------- */
  function genExpenses(){
    var out = [];
    MONTHS_2026.forEach(function(m){
      var count = m===9 ? 4 : randInt(5,8);
      for(var i=0;i<count;i++){
        var day = randInt(1, DAYS_IN_MONTH[m]);
        var cat = pick(EXPENSE_CATEGORIES);
        var recurring = ["Software & Tools","Utilities"].indexOf(cat)>=0 && rnd()<0.6;
        var amt = cat==="Subcontractors" ? randMoney(200,1400) : cat==="Equipment" ? randMoney(80,1200) : randMoney(12,420);
        var status = pickWeighted([["paid",8],["pending",1.4],["overdue",0.6]]);
        var proj = rnd()<0.65 ? projectForIndex(i+m) : null;
        out.push({
          id: uid("exp"),
          tenant_id:"t1", project_id:proj,
          description: pick(EXP_DESCRIPTIONS[cat]),
          category: cat,
          amount: amt,
          date: isoDate(2026,m,day),
          status: status,
          recurring: recurring,
          recurring_interval: recurring ? "monthly" : null,
          payment_method: pick(["Business Checking","Visa Debit","Mastercard Corporate"]),
          notes: "",
          created_at: isoDate(2026,m,day)
        });
      }
    });
    return out.sort(function(a,b){ return a.date<b.date?1:-1; });
  }

  var EXP_DESCRIPTIONS = {
    "Software & Tools": ["Figma subscription","Adobe Creative Cloud","Notion Team plan","GitHub Pro","Google Workspace","Vercel hosting","1Password Teams","Zoom Pro"],
    "Office & Supplies": ["Printer paper & ink","Desk organizer set","Notebooks & pens","Standing desk mat","Coffee for office"],
    "Travel": ["Client visit — train ticket","Flight to Barcelona","Taxi to client meeting","Hotel — conference","Parking — client site"],
    "Marketing": ["LinkedIn Ads campaign","Portfolio site renewal","Business cards printing","Google Ads credit"],
    "Professional Services": ["Accountant monthly fee","Legal consultation","Notary fees","Business insurance premium"],
    "Taxes & Fees": ["Quarterly VAT payment","Self-employment social security","Bank account maintenance fee","Freelancer association dues"],
    "Equipment": ["27-inch monitor","Mechanical keyboard","External SSD 2TB","Webcam upgrade","Office chair"],
    "Utilities": ["Internet — Movistar Fibra","Coworking desk membership","Mobile phone plan","Electricity — home office"],
    "Subcontractors": ["Illustrator — freelance","Backend dev support","Copywriter — landing page","QA tester — sprint"]
  };

  function genRevenues(){
    var out = [];
    MONTHS_2026.forEach(function(m){
      var count = m===9 ? 3 : randInt(3,5);
      for(var i=0;i<count;i++){
        var day = randInt(1, DAYS_IN_MONTH[m]);
        var cat = pick(REVENUE_CATEGORIES);
        var proj = pick(PROJECTS).id;
        var recurring = cat==="Retainer";
        out.push({
          id: uid("rev"),
          tenant_id:"t1", project_id:proj,
          description: cat + " — " + PROJECTS.find(function(p){return p.id===proj;}).client,
          category: cat,
          amount: cat==="Retainer" ? randMoney(1200,2400) : randMoney(600,4800),
          date: isoDate(2026,m,day),
          status: pickWeighted([["received",7],["pending",1.6],["overdue",0.5]]),
          recurring: recurring,
          recurring_interval: recurring ? "monthly" : null,
          created_at: isoDate(2026,m,day)
        });
      }
    });
    return out.sort(function(a,b){ return a.date<b.date?1:-1; });
  }

  function genInvoices(){
    var out = [];
    var n = 1042;
    MONTHS_2026.forEach(function(m){
      var count = m===9 ? 3 : randInt(3,6);
      for(var i=0;i<count;i++){
        var day = randInt(1, DAYS_IN_MONTH[m]);
        var direction = rnd()<0.82 ? "outgoing" : "incoming";
        var type = pick(["service","product"]);
        var proj = pick(PROJECTS);
        var issueDate = isoDate(2026,m,day);
        var due = addDays(issueDate, pick([15,30,30,45]));
        var overdue = due < today();
        var status = direction==="outgoing"
          ? pickWeighted([["paid",6],["unpaid",2],["overdue", overdue?2:0.2],["draft",0.8]])
          : pickWeighted([["paid",5],["unpaid",2],["overdue", overdue?1.5:0.2]]);
        var items = genInvoiceItems();
        var subtotal = items.reduce(function(s,it){return s+it.qty*it.unit_price;},0);
        var tax = Math.round(subtotal*0.21*100)/100;
        var total = Math.round((subtotal+tax)*100)/100;
        out.push({
          id: uid("inv"),
          number: "INV-2026-" + (n++),
          tenant_id:"t1", project_id: proj.id,
          client: direction==="outgoing" ? proj.client : "Studio Supplies Co.",
          direction: direction,
          type: type,
          status: status,
          items: items,
          subtotal: subtotal, tax: tax, amount: total,
          date: issueDate, due_date: due,
          created_at: issueDate
        });
      }
    });
    return out.sort(function(a,b){ return a.date<b.date?1:-1; });
  }
  var ITEM_NAMES = ["Discovery & strategy session","UI design — homepage","UI design — inner pages","Frontend development sprint","Backend API integration","Brand guidelines document","Logo design & variations","Monthly retainer hours","QA & bug fixing pass","Hosting & deployment setup","Copywriting — 5 pages","Motion graphics — intro video"];
  function genInvoiceItems(){
    var count = randInt(1,4);
    var items = [];
    for(var i=0;i<count;i++){
      items.push({ name: pick(ITEM_NAMES), qty: randInt(1,3), unit_price: randMoney(120,950) });
    }
    return items;
  }

  function genPayments(invoices){
    var out = [];
    invoices.forEach(function(inv){
      if(inv.status==="paid"){
        out.push({
          id: uid("pay"),
          tenant_id:"t1", invoice_id: inv.id, project_id: inv.project_id,
          amount: inv.amount,
          date: addDays(inv.date, randInt(1,20)),
          payment_method: pick(["Bank transfer","Credit card","Direct debit","PayPal"]),
          status:"completed",
          created_at: inv.date
        });
      } else if(inv.status==="unpaid" && rnd()<0.3){
        out.push({
          id: uid("pay"),
          tenant_id:"t1", invoice_id: inv.id, project_id: inv.project_id,
          amount: Math.round(inv.amount*0.5*100)/100,
          date: addDays(inv.date, randInt(1,10)),
          payment_method: pick(["Bank transfer","Credit card"]),
          status:"partial",
          created_at: inv.date
        });
      }
    });
    return out.sort(function(a,b){ return a.date<b.date?1:-1; });
  }

  function genTransactions(expenses,revenues){
    var out = [];
    expenses.forEach(function(e){
      if(e.status==="paid"){
        out.push({
          id: uid("txn"),
          tenant_id:"t1",
          bank_account_id: e.payment_method==="Business Checking"?"acc1": e.payment_method==="Visa Debit"?"acc1":"acc2",
          amount: -Math.abs(e.amount),
          date: e.date,
          type:"expense",
          description: e.description,
          category: e.category,
          reconciled: rnd()<0.85,
          linked_expense_id: e.id
        });
      }
    });
    revenues.forEach(function(r){
      if(r.status==="received"){
        out.push({
          id: uid("txn"),
          tenant_id:"t1",
          bank_account_id:"acc1",
          amount: Math.abs(r.amount),
          date: r.date,
          type:"income",
          description: r.description,
          category: r.category,
          reconciled: rnd()<0.85,
          linked_revenue_id: r.id
        });
      }
    });
    return out.sort(function(a,b){ return a.date<b.date?1:-1; });
  }

  function genBudgets(){
    return [
      { id: uid("bud"), tenant_id:"t1", project_id:null, name:"Software & Tools", category:"Software & Tools", allocated_amount:350, start_date:"2026-09-01", end_date:"2026-09-30" },
      { id: uid("bud"), tenant_id:"t1", project_id:null, name:"Marketing", category:"Marketing", allocated_amount:500, start_date:"2026-09-01", end_date:"2026-09-30" },
      { id: uid("bud"), tenant_id:"t1", project_id:null, name:"Travel", category:"Travel", allocated_amount:300, start_date:"2026-09-01", end_date:"2026-09-30" },
      { id: uid("bud"), tenant_id:"t1", project_id:null, name:"Equipment", category:"Equipment", allocated_amount:800, start_date:"2026-09-01", end_date:"2026-09-30" },
      { id: uid("bud"), tenant_id:"t1", project_id:null, name:"Professional Services", category:"Professional Services", allocated_amount:400, start_date:"2026-09-01", end_date:"2026-09-30" }
    ];
  }

  function genPlans(){
    return [
      {
        id: uid("plan"), tenant_id:"t1", project_id:null,
        title:"Q4 2026 Growth Plan",
        description:"Forecast for expanding retainer clients and controlling tooling spend through year end.",
        status:"active",
        created_at:"2026-08-15",
        planned_revenues:[
          { id: uid("prev"), amount:2400, date:"2026-10-05", category:"Retainer", recurring:true, recurring_interval:"monthly" },
          { id: uid("prev"), amount:5200, date:"2026-10-20", category:"Web Development", recurring:false },
          { id: uid("prev"), amount:2400, date:"2026-11-05", category:"Retainer", recurring:true, recurring_interval:"monthly" },
          { id: uid("prev"), amount:2400, date:"2026-12-05", category:"Retainer", recurring:true, recurring_interval:"monthly" }
        ],
        planned_expenses:[
          { id: uid("pexp"), amount:350, date:"2026-10-01", category:"Software & Tools", recurring:true, recurring_interval:"monthly" },
          { id: uid("pexp"), amount:900, date:"2026-10-15", category:"Subcontractors", recurring:false },
          { id: uid("pexp"), amount:350, date:"2026-11-01", category:"Software & Tools", recurring:true, recurring_interval:"monthly" },
          { id: uid("pexp"), amount:350, date:"2026-12-01", category:"Software & Tools", recurring:true, recurring_interval:"monthly" }
        ]
      },
      {
        id: uid("plan"), tenant_id:"t1", project_id:"p2",
        title:"Luma Wellness — Rebrand Budget",
        description:"Planned spend and expected milestone billing for the Luma Wellness identity project.",
        status:"active",
        created_at:"2026-07-01",
        planned_revenues:[
          { id: uid("prev"), amount:3200, date:"2026-09-15", category:"Design Services", recurring:false },
          { id: uid("prev"), amount:2800, date:"2026-10-10", category:"Design Services", recurring:false }
        ],
        planned_expenses:[
          { id: uid("pexp"), amount:600, date:"2026-09-10", category:"Subcontractors", recurring:false }
        ]
      },
      {
        id: uid("plan"), tenant_id:"t1", project_id:null,
        title:"Annual Tax Reserve Plan",
        description:"Set-aside plan for quarterly VAT and self-employment contributions.",
        status:"draft",
        created_at:"2026-06-10",
        planned_revenues:[],
        planned_expenses:[
          { id: uid("pexp"), amount:1450, date:"2026-10-20", category:"Taxes & Fees", recurring:false },
          { id: uid("pexp"), amount:1450, date:"2027-01-20", category:"Taxes & Fees", recurring:false }
        ]
      }
    ];
  }

  function genReports(){
    return [
      { id: uid("rep"), tenant_id:"t1", project_id:null, title:"August 2026 Profit & Loss", type:"profit_loss", created_at:"2026-09-01", period:"2026-08", summary:"Net profit of €4,120 across all projects, down 6% from July due to increased subcontractor spend." },
      { id: uid("rep"), tenant_id:"t1", project_id:null, title:"Q2 2026 Cash Flow Summary", type:"cash_flow", created_at:"2026-07-05", period:"2026-Q2", summary:"Positive cash flow of €9,860 with strongest inflow in June from the Nord Consulting retainer." },
      { id: uid("rep"), tenant_id:"t1", project_id:"p1", title:"Acme Retail — Project Financials", type:"project", created_at:"2026-08-20", period:"project", summary:"Project is 12% under budget with two outstanding invoices totaling €3,400." },
      { id: uid("rep"), tenant_id:"t1", project_id:null, title:"2026 YTD Expense Breakdown", type:"expense_breakdown", created_at:"2026-09-02", period:"2026-YTD", summary:"Software & Tools and Subcontractors remain the two largest expense categories year to date." }
    ];
  }

  function genNotifications(){
    return [
      { id: uid("ntf"), title:"Invoice INV-2026-1071 is overdue", body:"Fenwick Logistics — €2,340 due 5 days ago.", date:"2026-09-02", read:false, kind:"warning" },
      { id: uid("ntf"), title:"Payment received", body:"Nord Consulting retainer payment of €2,200 recorded.", date:"2026-09-01", read:false, kind:"success" },
      { id: uid("ntf"), title:"Budget alert: Marketing", body:"You've used 88% of your September Marketing budget.", date:"2026-08-30", read:false, kind:"warning" },
      { id: uid("ntf"), title:"New report available", body:"August 2026 Profit & Loss report has been generated.", date:"2026-08-29", read:true, kind:"info" },
      { id: uid("ntf"), title:"Card expiring soon", body:"Mastercard Corporate **** 7735 expires in 7 months.", date:"2026-08-25", read:true, kind:"info" }
    ];
  }

  function genActivity(){
    return [
      { id: uid("act"), text:"Alex Rivera created invoice INV-2026-1078 for Acme Retail", date:"2026-09-03" },
      { id: uid("act"), text:"Marta Solis reconciled 6 transactions on Business Checking", date:"2026-09-02" },
      { id: uid("act"), text:"Alex Rivera logged an expense: Figma subscription (€15.00)", date:"2026-09-02" },
      { id: uid("act"), text:"Payment of €2,200 received against retainer invoice", date:"2026-09-01" },
      { id: uid("act"), text:"Alex Rivera generated August 2026 Profit & Loss report", date:"2026-09-01" },
      { id: uid("act"), text:"Alex Rivera updated budget: Marketing (€500 allocated)", date:"2026-08-30" }
    ];
  }

  /* ---------------- build & persist ---------------- */
  function buildFreshData(){
    var expenses = genExpenses();
    var revenues = genRevenues();
    var invoices = genInvoices();
    var payments = genPayments(invoices);
    var transactions = genTransactions(expenses, revenues);
    return {
      _v: SCHEMA_VERSION,
      tenant: TENANT,
      currentUserId: "u1",
      users: TEAM_USERS,
      projects: PROJECTS,
      bankAccounts: BANK_ACCOUNTS,
      cards: CARDS,
      expenses: expenses,
      revenues: revenues,
      invoices: invoices,
      payments: payments,
      transactions: transactions,
      budgets: genBudgets(),
      plans: genPlans(),
      reports: genReports(),
      notifications: genNotifications(),
      activity: genActivity(),
      prefs: { theme:"light", currentProjectId:"all", notifyEmail:true, notifyOverdue:true, notifyBudget:true, twoFactor:false }
    };
  }

  function load(){
    try{
      var raw = localStorage.getItem(DB_KEY);
      if(raw){
        var parsed = JSON.parse(raw);
        if(parsed && parsed._v === SCHEMA_VERSION) return parsed;
      }
    }catch(e){ /* fall through to fresh */ }
    var fresh = buildFreshData();
    persist(fresh);
    return fresh;
  }

  function persist(state){
    try{ localStorage.setItem(DB_KEY, JSON.stringify(state)); }catch(e){ /* storage full/unavailable — ignore */ }
  }

  var STATE = load();

  var DB = {
    KEY: DB_KEY,
    state: STATE,
    save: function(){ persist(STATE); },
    reset: function(){ STATE = buildFreshData(); persist(STATE); return STATE; },
    all: function(col){ return STATE[col] || []; },
    get: function(col,id){ return (STATE[col]||[]).find(function(x){return x.id===id;}); },
    create: function(col,obj,prefix){
      if(!obj.id) obj.id = uid(prefix||col.slice(0,3));
      if(!obj.created_at) obj.created_at = today();
      STATE[col] = STATE[col] || [];
      STATE[col].unshift(obj);
      this.save();
      return obj;
    },
    update: function(col,id,patch){
      var item = this.get(col,id);
      if(!item) return null;
      Object.assign(item, patch);
      this.save();
      return item;
    },
    remove: function(col,id){
      STATE[col] = (STATE[col]||[]).filter(function(x){return x.id!==id;});
      this.save();
    },
    prefs: function(){ return STATE.prefs; },
    setPref: function(k,v){ STATE.prefs[k]=v; this.save(); },
    currentUser: function(){ return STATE.users.find(function(u){return u.id===STATE.currentUserId;}); },
    projectName: function(id){
      if(!id) return "General (no project)";
      var p = STATE.projects.find(function(x){return x.id===id;});
      return p ? p.name : "Unknown project";
    }
  };

  window.Contapop = window.Contapop || {};
  window.Contapop.DB = DB;
  window.Contapop.helpers = { today: today, addDays: addDays, MONTH_NAMES: MONTH_NAMES, MONTHS_2026: MONTHS_2026, EXPENSE_CATEGORIES: EXPENSE_CATEGORIES, REVENUE_CATEGORIES: REVENUE_CATEGORIES };
})();
