import { useEffect, useState, type ReactNode } from 'react';
import './styles.css';

/* ---------------- icons (ported verbatim from the prototype's icon set) ---------------- */
const ICONS: Record<string, string> = {
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
  chevronLeft: '<path d="M15 18l-6-6 6-6"/>',
  chevronRight: '<path d="M9 18l6-6-6-6"/>',
  alert: '<path d="M10.3 3.86l-8.2 14.2A1 1 0 003 19.5h18a1 1 0 00.87-1.44l-8.2-14.2a1 1 0 00-1.74 0z"/><path d="M12 9v4M12 17h.01"/>',
  target: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.4"/>',
  checkCircle: '<circle cx="12" cy="12" r="9"/><path d="M8 12l3 3 5-6"/>',
  fileText: '<path d="M6 2h9l5 5v15H6z"/><path d="M14 2v5h5"/><path d="M9 13h6M9 17h6"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 8h.01M11 12h1v5h1"/>',
};
function Icon({ name, className = '' }: { name: string; className?: string }) {
  return <svg className={`icon ${className}`} viewBox="0 0 24 24" dangerouslySetInnerHTML={{ __html: ICONS[name] ?? ICONS.info }} />;
}

/* ---------------- status badges ---------------- */
const STATUS_MAP: Record<string, [string, string]> = {
  paid: ['green', 'Paid'], received: ['green', 'Received'], completed: ['green', 'Completed'], active: ['green', 'Active'],
  pending: ['amber', 'Pending'], draft: ['gray', 'Draft'], partial: ['blue', 'Partial'], overdue: ['red', 'Overdue'],
};
function Badge({ status, brand }: { status: string; brand?: string }) {
  if (brand) return <span className="badge badge-brand">{brand}</span>;
  const [color, label] = STATUS_MAP[status] ?? ['gray', status];
  return <span className={`badge badge-${color}`}>{label}</span>;
}

/* ---------------- data (demo values only — same shape as the prototype's seed) ---------------- */
type Status = 'paid' | 'pending' | 'overdue' | 'received' | 'completed' | 'partial' | 'draft' | 'active';
type Item = { id: string; description: string; category: string; amount: number; date: string; status: Status; projectId?: string; client?: string; method?: string };
const projects = [
  { id: 'p1', name: 'Website Redesign - Acme Retail' },
  { id: 'p2', name: 'Brand Identity - Luma Wellness' },
  { id: 'p3', name: 'Retainer - Nord Consulting' },
  { id: 'p4', name: 'Mobile App - Fenwick Logistics' },
];
const expenses: Item[] = [
  { id: 'exp1', description: 'Figma subscription', category: 'Software & Tools', amount: 15, date: '2026-09-02', status: 'paid', projectId: 'p1' },
  { id: 'exp2', description: 'Backend dev support', category: 'Subcontractors', amount: 840, date: '2026-08-28', status: 'paid', projectId: 'p2' },
  { id: 'exp3', description: 'Google Ads credit', category: 'Marketing', amount: 220, date: '2026-08-25', status: 'pending', projectId: 'p1' },
  { id: 'exp4', description: 'Accountant monthly fee', category: 'Professional Services', amount: 180, date: '2026-08-15', status: 'paid' },
];
const revenues: Item[] = [
  { id: 'rev1', description: 'Retainer - Nord Consulting', category: 'Retainer', amount: 2200, date: '2026-09-01', status: 'received', projectId: 'p3' },
  { id: 'rev2', description: 'Design Services - Luma Wellness', category: 'Design Services', amount: 3200, date: '2026-08-27', status: 'received', projectId: 'p2' },
  { id: 'rev3', description: 'Web Development - Acme Retail', category: 'Web Development', amount: 4200, date: '2026-08-20', status: 'pending', projectId: 'p1' },
];
const invoices: Item[] = [
  { id: 'inv1', description: 'INV-2026-1078', client: 'Acme Retail', category: 'Service', amount: 4200, date: '2026-09-03', status: 'pending', projectId: 'p1' },
  { id: 'inv2', description: 'INV-2026-1071', client: 'Fenwick Logistics', category: 'Service', amount: 2340, date: '2026-08-01', status: 'overdue', projectId: 'p4' },
  { id: 'inv3', description: 'INV-2026-1069', client: 'Nord Consulting', category: 'Service', amount: 2200, date: '2026-08-01', status: 'paid', projectId: 'p3' },
];
const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR' }).format(value);
const moneySigned = (value: number) => (value >= 0 ? '+' : '') + money(value);
const date = (value: string) => new Date(`${value}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
const projectName = (id?: string) => projects.find((p) => p.id === id)?.name ?? 'General';

const NAV_ITEMS: { section?: string; route?: string; label?: string; icon?: string }[] = [
  { route: 'home', label: 'Home', icon: 'home' },
  { route: 'overview', label: 'Financial Overview', icon: 'overview' },
  { section: 'Money' },
  { route: 'expenses', label: 'Expenses', icon: 'expenses' },
  { route: 'revenues', label: 'Revenues', icon: 'revenues' },
  { route: 'invoices', label: 'Invoices', icon: 'invoices' },
  { route: 'payments', label: 'Payments', icon: 'payments' },
  { route: 'transactions', label: 'Transactions', icon: 'transactions' },
  { section: 'Plan & analyze' },
  { route: 'reports', label: 'Reports', icon: 'reports' },
  { route: 'plans', label: 'Plans', icon: 'plans' },
  { section: 'Account' },
  { route: 'settings', label: 'Settings', icon: 'settings' },
  { route: 'user', label: 'My Profile', icon: 'user' },
];

const TITLES: Record<string, [string, string]> = {
  home: ['Home', 'Your financial snapshot at a glance'],
  overview: ['Financial Overview', 'Monitor revenue, expenses and budget health'],
  expenses: ['Expenses', 'All outgoing costs across your projects'],
  revenues: ['Revenues', 'All income across your projects'],
  invoices: ['Invoices', 'Incoming and outgoing billing documents'],
  payments: ['Payments', 'Payments recorded against invoices'],
  transactions: ['Transactions', 'All bank account activity in one place'],
  reports: ['Reports', 'Financial reports and analysis'],
  plans: ['Plans', 'Financial forecasts and strategies'],
  settings: ['Settings', 'Manage your account, preferences and security'],
  user: ['My Profile', 'Your personal information and activity'],
};

/* ---------------- shared building blocks ---------------- */
function Card({ children, className = '', style }: { children: ReactNode; className?: string; style?: React.CSSProperties }) {
  return <section className={`card ${className}`} style={style}>{children}</section>;
}
function StatCard({ label, value, icon, iconBg, delta, sub }: { label: string; value: string; icon: string; iconBg: string; delta?: number; sub?: string }) {
  return (
    <Card className="stat-card">
      <div className="stat-top">
        <span className="stat-label">{label}</span>
        <span className={`stat-icon ${iconBg}`}><Icon name={icon} /></span>
      </div>
      <div className="stat-value">{value}</div>
      {delta !== undefined ? (
        <div className={`stat-delta ${delta >= 0 ? 'up' : 'down'}`}>{delta >= 0 ? '▲' : '▼'} {Math.abs(delta)}% vs Aug</div>
      ) : sub ? (
        <div className="stat-delta" style={{ color: 'var(--text-faint)' }}>{sub}</div>
      ) : null}
    </Card>
  );
}

type Column<T> = { key: string; label: string; align?: 'right'; render: (item: T) => ReactNode };
function ListScreen<T extends { id: string }>({
  heading, subheading, items, columns, searchPlaceholder, searchFields, primaryLabel, onToast,
}: {
  heading: string; subheading: string; items: T[]; columns: Column<T>[]; searchPlaceholder: string;
  searchFields: (keyof T)[]; primaryLabel: string; onToast: (text: string) => void;
}) {
  const [search, setSearch] = useState('');
  const filtered = items.filter((item) => {
    if (!search) return true;
    const q = search.toLowerCase();
    return searchFields.some((f) => String(item[f] ?? '').toLowerCase().includes(q));
  });
  return (
    <>
      <div className="page-head">
        <div><div className="section-title">{heading}</div><p className="muted">{subheading}</p></div>
        <div className="page-head-actions">
          <button className="btn btn-primary" onClick={() => onToast(`${primaryLabel} is a prototype action.`)}>
            <Icon name="plus" /><span>{primaryLabel}</span>
          </button>
        </div>
      </div>
      <div className="toolbar">
        <div className="search-box">
          <Icon name="search" />
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={searchPlaceholder} />
        </div>
        <div className="toolbar-spacer" />
      </div>
      <div className="result-count">{filtered.length} {filtered.length === 1 ? 'result' : 'results'}</div>
      {filtered.length === 0 ? (
        <Card>
          <div className="empty-state">
            <Icon name="info" />
            <div className="msg">No results found</div>
            <div className="sub">Try adjusting your search or filters.</div>
          </div>
        </Card>
      ) : (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  {columns.map((c) => <th key={c.key} className={c.align === 'right' ? 'num' : ''}>{c.label}</th>)}
                  <th />
                </tr>
              </thead>
              <tbody>
                {filtered.map((item) => (
                  <tr className="clickable" key={item.id} onClick={() => onToast('Record details opened.')}>
                    {columns.map((c) => <td key={c.key} className={c.align === 'right' ? 'num' : ''}>{c.render(item)}</td>)}
                    <td>
                      <div className="row-actions">
                        <button className="icon-btn btn-sm" title="Edit" onClick={(e) => { e.stopPropagation(); onToast('Edit is a prototype action.'); }}><Icon name="edit" /></button>
                        <button className="icon-btn btn-sm" title="Delete" onClick={(e) => { e.stopPropagation(); onToast('Delete is a prototype action.'); }}><Icon name="trash" /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pagination" style={{ padding: '12px 14px 14px' }}>
            <span>Page 1 of 1</span>
            <div className="pg-btns">
              <button disabled><Icon name="chevronLeft" /></button>
              <button disabled><Icon name="chevronRight" /></button>
            </div>
          </div>
        </Card>
      )}
    </>
  );
}

/* ---------------- app shell ---------------- */
function App() {
  const [route, setRoute] = useState('home');
  const [theme, setTheme] = useState<'light' | 'dark'>('light');
  const [project, setProject] = useState('all');
  const [mobileNav, setMobileNav] = useState(false);
  const [search, setSearch] = useState('');
  const [toast, setToast] = useState('');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); }, [theme]);
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(''), 3200);
    return () => clearTimeout(timer);
  }, [toast]);

  const navigate = (next: string) => { setRoute(next); setMobileNav(false); };
  const [title, subtitle] = TITLES[route] ?? TITLES.home;
  const listItems = route === 'expenses' ? expenses : route === 'revenues' ? revenues : invoices;

  const projectSelect = (
    <select value={project} onChange={(e) => setProject(e.target.value)}>
      <option value="all">All Projects</option>
      {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
    </select>
  );

  return (
    <div className={`app ${mobileNav ? 'nav-open' : ''}`}>
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-mark">C</span>
          <span className="brand-name">Contapop</span>
        </div>
        <div className="sidebar-project-select">{projectSelect}</div>
        <nav className="sidebar-nav">
          {NAV_ITEMS.map((item) =>
            item.section ? (
              <div className="nav-section-label" key={item.section}>{item.section}</div>
            ) : (
              <button
                key={item.route}
                className={`nav-item ${route === item.route ? 'active' : ''}`}
                onClick={() => navigate(item.route!)}
              >
                <Icon name={item.icon!} /><span>{item.label}</span>
              </button>
            )
          )}
        </nav>
        <div className="sidebar-footer">
          <div className="sidebar-tenant">
            <div className="tenant-mark">A</div>
            <div className="tenant-meta">
              <div className="name">Alex Rivera Freelance Studio</div>
              <div className="role">Owner · Pro plan</div>
            </div>
          </div>
        </div>
      </aside>

      {mobileNav && <div className="sidebar-overlay" onClick={() => setMobileNav(false)} />}

      <div className="main">
        <header className="topbar">
          <button className="icon-btn only-mobile" aria-label="Toggle menu" onClick={() => setMobileNav(true)}><Icon name="menu" /></button>
          <div className="topbar-title"><h1>{title}</h1><p className="muted">{subtitle}</p></div>
          <div className="topbar-spacer" />
          <div className="project-select">{projectSelect}</div>
          <label className="topbar-search">
            <Icon name="search" />
            <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search everything…" />
          </label>
          <button className="icon-btn" aria-label="Notifications" onClick={() => setToast('3 unread notification(s).')}>
            <Icon name="bell" /><span className="badge-dot" />
          </button>
          <button className="avatar-btn" onClick={() => navigate('user')}>AR</button>
        </header>

        <main className="content">
          {route === 'home' && <Home onNavigate={navigate} />}
          {route === 'overview' && <Overview />}
          {['expenses', 'revenues', 'invoices'].includes(route) && (
            <ListScreen
              heading={title} subheading={subtitle}
              items={listItems.filter((i) => `${i.description} ${i.client ?? ''}`.toLowerCase().includes(search.toLowerCase()))}
              searchPlaceholder={`Search ${route}…`} searchFields={['description', 'client']}
              primaryLabel={`New ${route.slice(0, -1)}`} onToast={setToast}
              columns={[
                { key: 'description', label: route === 'invoices' ? 'Description / project' : 'Description / project', render: (i) => (<><div className="cell-title">{i.description}</div><div className="cell-sub">{i.client ?? projectName(i.projectId)}</div></>) },
                { key: 'category', label: 'Category', render: (i) => i.category },
                { key: 'amount', label: 'Amount', align: 'right', render: (i) => money(i.amount) },
                { key: 'date', label: 'Date', render: (i) => date(i.date) },
                { key: 'status', label: 'Status', render: (i) => <Badge status={i.status} /> },
              ]}
            />
          )}
          {route === 'transactions' && (
            <ListScreen
              heading={title} subheading={subtitle}
              items={[...expenses.filter((i) => i.status === 'paid'), ...revenues.filter((i) => i.status === 'received')]}
              searchPlaceholder="Search transactions…" searchFields={['description']}
              primaryLabel="New transaction" onToast={setToast}
              columns={[
                { key: 'description', label: 'Description / project', render: (i) => (<><div className="cell-title">{i.description}</div><div className="cell-sub">{projectName(i.projectId)}</div></>) },
                { key: 'category', label: 'Category', render: (i) => i.category },
                { key: 'amount', label: 'Amount', align: 'right', render: (i) => money(i.amount) },
                { key: 'date', label: 'Date', render: (i) => date(i.date) },
                { key: 'status', label: 'Status', render: (i) => <Badge status={i.status} /> },
              ]}
            />
          )}
          {route === 'payments' && (
            <ListScreen
              heading={title} subheading={subtitle}
              items={invoices.filter((i) => i.status === 'paid')}
              searchPlaceholder="Search payments…" searchFields={['description', 'client']}
              primaryLabel="Record payment" onToast={setToast}
              columns={[
                { key: 'description', label: 'Invoice', render: (i) => (<><div className="cell-title">{i.description}</div><div className="cell-sub">{i.client}</div></>) },
                { key: 'amount', label: 'Amount', align: 'right', render: (i) => money(i.amount) },
                { key: 'date', label: 'Date', render: (i) => date(i.date) },
                { key: 'method', label: 'Method', render: () => 'Bank transfer' },
                { key: 'status', label: 'Status', render: (i) => <Badge status={i.status} /> },
              ]}
            />
          )}
          {route === 'reports' && <Reports onToast={setToast} />}
          {route === 'plans' && <Plans onToast={setToast} />}
          {route === 'settings' && <Settings theme={theme} setTheme={setTheme} onToast={setToast} />}
          {route === 'user' && <Profile onToast={setToast} />}
        </main>
      </div>

      {toast && <div className="toast success"><Icon name="checkCircle" className="toast-icon" /><span>{toast}</span></div>}
    </div>
  );
}

function Home({ onNavigate }: { onNavigate: (route: string) => void }) {
  return (
    <>
      <div
        className="card card-pad"
        style={{ marginBottom: 20, background: 'linear-gradient(135deg,var(--brand-500),var(--brand-700))', color: '#fff', border: 'none', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 14 }}
      >
        <div>
          <div style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: '.06em', opacity: .85, fontWeight: 700 }}>Welcome back</div>
          <div style={{ fontSize: 20, fontWeight: 700, marginTop: 3 }}>Alex Rivera</div>
          <div style={{ fontSize: 12.5, opacity: .85, marginTop: 4 }}>Here is what is happening across all your projects this month.</div>
        </div>
        <div style={{ display: 'flex', gap: 22 }}>
          <div><div style={{ fontSize: 11, opacity: .8 }}>Net (Sep)</div><div style={{ fontSize: 19, fontWeight: 700 }}>{moneySigned(2165)}</div></div>
          <div><div style={{ fontSize: 11, opacity: .8 }}>Outstanding</div><div style={{ fontSize: 19, fontWeight: 700 }}>{money(6540)}</div></div>
        </div>
      </div>

      <div className="grid grid-4" style={{ marginBottom: 20 }}>
        <StatCard label="Revenue (Sep)" value={money(5400)} icon="revenues" iconBg="icon-bg-green" delta={12.4} />
        <StatCard label="Expenses (Sep)" value={money(1035)} icon="expenses" iconBg="icon-bg-red" delta={-8.1} />
        <StatCard label="Budget used" value="62%" icon="target" iconBg="icon-bg-amber" sub="On track this month" />
        <StatCard label="Overdue invoices" value="1" icon="alert" iconBg="icon-bg-blue" sub={`${money(2340)} total`} />
      </div>

      <div className="grid grid-2" style={{ alignItems: 'start', gap: 16 }}>
        <Card>
          <div className="card-head"><h3>Recent activity</h3><button className="link-btn" onClick={() => onNavigate('transactions')}>View transactions</button></div>
          <div className="card-body">
            <div className="timeline">
              {[
                ['Alex Rivera created invoice INV-2026-1078 for Acme Retail', '1d ago'],
                ['Marta Solis reconciled 6 transactions on Business Checking', '2d ago'],
                ['Alex Rivera logged an expense: Figma subscription (€15.00)', '3d ago'],
                ['Payment of €2,200 received against retainer invoice', '4d ago'],
              ].map(([t, d]) => (
                <div className="timeline-item" key={t}><span className="timeline-dot" /><div><div className="t">{t}</div><div className="d">{d}</div></div></div>
              ))}
            </div>
          </div>
        </Card>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card>
            <div className="card-head"><h3>Alerts</h3></div>
            <div className="card-body">
              <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', padding: '8px 0' }}>
                <span className="stat-icon icon-bg-red" style={{ width: 26, height: 26 }}><Icon name="alert" /></span>
                <span style={{ fontSize: 12.5, paddingTop: 3 }}>1 invoice(s) overdue, totaling {money(2340)}</span>
              </div>
              <div className="divider" style={{ margin: 0 }} />
              <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', padding: '8px 0' }}>
                <span className="stat-icon icon-bg-amber" style={{ width: 26, height: 26 }}><Icon name="target" /></span>
                <span style={{ fontSize: 12.5, paddingTop: 3 }}>You've used 88% of your Marketing budget</span>
              </div>
            </div>
          </Card>
          <Card>
            <div className="card-head"><h3>Quick access</h3></div>
            <div className="card-body">
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <button className="btn" style={{ justifyContent: 'flex-start' }} onClick={() => onNavigate('expenses')}><Icon name="expenses" /><span>New expense</span></button>
                <button className="btn" style={{ justifyContent: 'flex-start' }} onClick={() => onNavigate('invoices')}><Icon name="invoices" /><span>New invoice</span></button>
                <button className="btn" style={{ justifyContent: 'flex-start' }} onClick={() => onNavigate('reports')}><Icon name="reports" /><span>View reports</span></button>
                <button className="btn" style={{ justifyContent: 'flex-start' }} onClick={() => onNavigate('overview')}><Icon name="overview" /><span>Financial overview</span></button>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}

function Overview() {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep'];
  const categories = [
    { name: 'Subcontractors', pct: 43, color: 'var(--brand-500)' },
    { name: 'Software & Tools', pct: 24, color: 'var(--brand-400)' },
    { name: 'Marketing', pct: 18, color: 'var(--blue-500)' },
    { name: 'Professional Services', pct: 15, color: 'var(--amber-500)' },
  ];
  const budgets = [
    { name: 'Marketing budget', pct: 88 }, { name: 'Software & Tools budget', pct: 52 }, { name: 'Subcontractors budget', pct: 34 },
  ];
  return (
    <>
      <div className="page-head">
        <div><div className="section-title">2026 year-to-date</div><p className="muted">Revenue, expenses and budget health across the year.</p></div>
        <div className="page-head-actions">
          <button className="btn"><Icon name="upload" /><span>Import bank statement</span></button>
          <button className="btn"><Icon name="download" /><span>Export summary</span></button>
        </div>
      </div>

      <div className="grid grid-3" style={{ marginBottom: 18 }}>
        <StatCard label="Total revenue" value={money(23140)} icon="revenues" iconBg="icon-bg-green" />
        <StatCard label="Total expenses" value={money(7815)} icon="expenses" iconBg="icon-bg-red" />
        <StatCard label="Net profit" value={moneySigned(15325)} icon="overview" iconBg="icon-bg-brand" />
      </div>

      <div className="grid grid-2" style={{ alignItems: 'start', gap: 16, gridTemplateColumns: '1.5fr 1fr' }}>
        <Card>
          <div className="card-head"><h3>Revenue vs. expenses by month</h3></div>
          <div className="card-body">
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 10, height: 180, borderBottom: '1px solid var(--border)', paddingBottom: 8 }}>
              {months.map((m, i) => (
                <div key={m} style={{ flex: 1, display: 'flex', alignItems: 'flex-end', gap: 2, height: '100%', position: 'relative' }}>
                  <div style={{ flex: 1, height: `${35 + i * 6}%`, background: 'var(--brand-500)', borderRadius: '3px 3px 0 0' }} />
                  <div style={{ flex: 1, height: `${20 + i * 4}%`, background: 'var(--red-500)', borderRadius: '3px 3px 0 0' }} />
                </div>
              ))}
            </div>
            <div style={{ display: 'flex', gap: 10, marginTop: 6 }}>
              {months.map((m) => <div key={m} style={{ flex: 1, textAlign: 'center', fontSize: 11, color: 'var(--text-muted)' }}>{m}</div>)}
            </div>
            <div className="chart-legend">
              <span className="item"><span className="sw" style={{ background: 'var(--brand-500)' }} />Revenue</span>
              <span className="item"><span className="sw" style={{ background: 'var(--red-500)' }} />Expenses</span>
            </div>
          </div>
        </Card>
        <Card>
          <div className="card-head"><h3>Expense breakdown</h3></div>
          <div className="card-body" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <div style={{
              width: 130, height: 130, borderRadius: '50%', position: 'relative', display: 'grid', placeItems: 'center', textAlign: 'center', fontWeight: 700,
              background: 'conic-gradient(var(--brand-500) 0 43%, var(--brand-400) 43% 67%, var(--blue-500) 67% 85%, var(--amber-500) 85%)',
            }}>
              <div style={{ position: 'absolute', inset: 22, background: 'var(--surface)', borderRadius: '50%' }} />
              <div style={{ position: 'relative', fontSize: 13 }}>{money(7815)}<div style={{ fontWeight: 400, color: 'var(--text-muted)', fontSize: 11 }}>Total expenses</div></div>
            </div>
            <div className="chart-legend" style={{ justifyContent: 'center' }}>
              {categories.map((c) => <span className="item" key={c.name}><span className="sw" style={{ background: c.color }} />{c.name} — {c.pct}%</span>)}
            </div>
          </div>
        </Card>
      </div>

      <div className="grid grid-2" style={{ alignItems: 'start', gap: 16, marginTop: 16 }}>
        <Card>
          <div className="card-head"><h3>Budget status — September</h3></div>
          <div className="card-body">
            {budgets.map((b) => (
              <div key={b.name} style={{ marginBottom: 12 }}>
                <div className="flex-between" style={{ marginBottom: 5 }}>
                  <span style={{ fontSize: 12.5, fontWeight: 600 }}>{b.name}</span>
                  <span style={{ fontSize: 12, color: 'var(--text-faint)' }}>{b.pct}%</span>
                </div>
                <div style={{ height: 8, borderRadius: 99, background: 'var(--surface-2)', overflow: 'hidden' }}>
                  <div style={{ height: '100%', width: `${b.pct}%`, borderRadius: 99, background: b.pct >= 100 ? 'var(--red-500)' : b.pct >= 80 ? 'var(--amber-500)' : 'var(--brand-500)' }} />
                </div>
              </div>
            ))}
          </div>
        </Card>
        <Card>
          <div className="card-head"><h3>Jump to</h3></div>
          <div className="card-body">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {(['expenses', 'revenues', 'invoices', 'payments'] as const).map((r) => (
                <button className="btn" style={{ justifyContent: 'flex-start' }} key={r}><Icon name={r} /><span>{r[0].toUpperCase() + r.slice(1)}</span></button>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </>
  );
}

function Reports({ onToast }: { onToast: (text: string) => void }) {
  const reports = [
    { id: 'r1', name: 'August 2026 Profit & Loss', type: 'Profit & Loss', period: 'Aug 2026', generated: 'Sep 2, 2026' },
    { id: 'r2', name: 'Q2 2026 Cash Flow Summary', type: 'Cash Flow', period: 'Q2 2026', generated: 'Sep 2, 2026' },
    { id: 'r3', name: 'Acme Retail - Project Financials', type: 'Project Financials', period: 'Project', generated: 'Sep 2, 2026' },
  ];
  return (
    <ListScreen
      heading="Reports" subheading="Generate and review financial reports."
      items={reports} searchPlaceholder="Search reports…" searchFields={['name']}
      primaryLabel="Generate report" onToast={onToast}
      columns={[
        { key: 'name', label: 'Report', render: (r) => (<><div className="cell-title">{r.name}</div><div className="cell-sub">All projects</div></>) },
        { key: 'type', label: 'Type', render: (r) => <Badge status="" brand={r.type} /> },
        { key: 'period', label: 'Period', render: (r) => r.period },
        { key: 'generated', label: 'Generated', render: (r) => r.generated },
      ]}
    />
  );
}

function Plans({ onToast }: { onToast: (text: string) => void }) {
  const plans = [
    { id: 'pl1', name: 'Q4 2026 Growth Plan', status: 'active' as const, net: 6800 },
    { id: 'pl2', name: 'Luma Wellness - Rebrand Budget', status: 'active' as const, net: 5400 },
    { id: 'pl3', name: 'Annual Tax Reserve Plan', status: 'draft' as const, net: -2900 },
  ];
  return (
    <>
      <div className="page-head">
        <div><div className="section-title">Plans</div><p className="muted">Financial forecasts and strategies.</p></div>
        <div className="page-head-actions">
          <button className="btn btn-primary" onClick={() => onToast('New plan is a prototype action.')}><Icon name="plus" /><span>New plan</span></button>
        </div>
      </div>
      <div className="grid grid-3">
        {plans.map((p) => (
          <Card className="card-pad" key={p.id}>
            <Badge status={p.status} />
            <h3 style={{ margin: '10px 0 6px' }}>{p.name}</h3>
            <p className="muted" style={{ marginBottom: 14 }}>Forecast planned revenues and expenses for your workspace.</p>
            <div style={{ fontWeight: 700, marginBottom: 14, color: p.net >= 0 ? 'var(--green-500)' : 'var(--red-500)' }}>{moneySigned(p.net)} planned net</div>
            <button className="btn btn-sm" onClick={() => onToast(`${p.name} opened.`)}>View plan</button>
          </Card>
        ))}
      </div>
    </>
  );
}

function Settings({ theme, setTheme, onToast }: { theme: 'light' | 'dark'; setTheme: (t: 'light' | 'dark') => void; onToast: (text: string) => void }) {
  const [tab, setTab] = useState<'account' | 'preferences' | 'security' | 'support'>('account');
  return (
    <>
      <div className="tabs">
        {(['account', 'preferences', 'security', 'support'] as const).map((t) => (
          <button key={t} className={`tab-btn ${tab === t ? 'active' : ''}`} onClick={() => setTab(t)}>{t[0].toUpperCase() + t.slice(1)}</button>
        ))}
      </div>
      {tab === 'account' && (
        <>
          <Card className="card-pad" style={{ maxWidth: 560 }}>
            <div className="section-title" style={{ marginBottom: 14 }}>Account information</div>
            <div className="form-grid">
              <div className="form-field full"><label>Full name</label><input defaultValue="Alex Rivera" /></div>
              <div className="form-field"><label>Email address</label><input defaultValue="admin@alicazum.com" /></div>
              <div className="form-field"><label>Phone number</label><input defaultValue="+34 611 220 934" /></div>
              <div className="form-field"><label>Role (read-only)</label><input defaultValue="Owner" disabled /></div>
            </div>
            <div style={{ marginTop: 16 }}><button className="btn btn-primary" onClick={() => onToast('Account information saved.')}>Save changes</button></div>
          </Card>
          <Card className="card-pad" style={{ maxWidth: 560, marginTop: 16 }}>
            <div className="section-title" style={{ marginBottom: 4 }}>Team members</div>
            <p className="muted" style={{ marginBottom: 14 }}>People with access to this tenant.</p>
            {[['Alex Rivera', 'admin@alicazum.com', 'Owner'], ['Marta Solis', 'marta@alicazum.com', 'Accountant']].map(([n, e, r]) => (
              <div className="flex-between" key={n} style={{ padding: '9px 0', borderBottom: '1px solid var(--border)' }}>
                <div className="flex gap-8" style={{ alignItems: 'center' }}>
                  <span className="avatar-sm">{n.split(' ').map((w) => w[0]).join('')}</span>
                  <div><div style={{ fontWeight: 600, fontSize: 13 }}>{n}</div><div className="muted">{e}</div></div>
                </div>
                <Badge status="" brand={r} />
              </div>
            ))}
          </Card>
        </>
      )}
      {tab === 'preferences' && (
        <Card className="card-pad" style={{ maxWidth: 620 }}>
          <div className="section-title" style={{ marginBottom: 14 }}>Appearance</div>
          <div className="settings-row">
            <div><div className="lbl">Theme</div><div className="desc">Choose how Contapop looks on this device.</div></div>
            <div className="theme-swatches">
              {(['light', 'dark'] as const).map((t) => (
                <button key={t} className={`theme-swatch ${theme === t ? 'active' : ''}`} title={t} onClick={() => setTheme(t)}>
                  <div className="sw-top" style={{ background: 'var(--brand-500)' }} />
                  <div className="sw-body">
                    <div className="sw-side" style={{ background: t === 'dark' ? '#14171a' : '#f5f6f5' }} />
                    <div style={{ flex: 1, background: t === 'dark' ? '#1b1f22' : '#ffffff' }} />
                  </div>
                </button>
              ))}
            </div>
          </div>
          <div className="divider" />
          <div className="section-title" style={{ marginBottom: 6 }}>Notifications</div>
          {[['Email notifications', 'Receive a daily digest by email.'], ['Overdue invoice alerts', 'Get notified when an invoice becomes overdue.'], ['Budget alerts', 'Get notified when a budget crosses 80% usage.']].map(([l, d]) => (
            <div className="settings-row" key={l}>
              <div><div className="lbl">{l}</div><div className="desc">{d}</div></div>
              <label className="toggle"><input type="checkbox" defaultChecked onChange={() => onToast('Preference saved.')} /><span className="track" /><span className="thumb" /></label>
            </div>
          ))}
        </Card>
      )}
      {tab === 'security' && (
        <>
          <Card className="card-pad" style={{ maxWidth: 620 }}>
            <div className="section-title" style={{ marginBottom: 6 }}>Security</div>
            <div className="settings-row">
              <div><div className="lbl">Two-factor authentication</div><div className="desc">Require a verification code at sign in.</div></div>
              <label className="toggle"><input type="checkbox" onChange={(e) => onToast(e.target.checked ? 'Two-factor authentication enabled.' : 'Two-factor authentication disabled.')} /><span className="track" /><span className="thumb" /></label>
            </div>
            <div className="settings-row">
              <div><div className="lbl">Password</div><div className="desc">Last changed 3 months ago.</div></div>
              <button className="btn btn-sm" onClick={() => onToast('Change password is a prototype action.')}>Change password</button>
            </div>
          </Card>
          <Card className="card-pad" style={{ maxWidth: 620, marginTop: 16 }}>
            <div className="section-title" style={{ marginBottom: 4 }}>Recent login activity</div>
            <p className="muted" style={{ marginBottom: 12 }}>Sessions on your account over the last two weeks.</p>
            <div className="table-wrap">
              <table>
                <thead><tr><th>Device</th><th>Location</th><th>Date</th></tr></thead>
                <tbody>
                  {[['Chrome on Windows', 'Madrid, Spain', '2026-09-03 09:12'], ['Contapop iOS App', 'Madrid, Spain', '2026-09-01 18:40'], ['Firefox on macOS', 'Barcelona, Spain', '2026-08-22 14:03']].map((row) => (
                    <tr key={row.join('-')}>{row.map((c) => <td key={c}>{c}</td>)}</tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      )}
      {tab === 'support' && (
        <div className="grid grid-2">
          {[
            ['info', 'Help Center', 'Browse guides and answers to common questions.', 'Visit Help Center'],
            ['fileText', 'Contact support', 'Reach our team for account or billing questions.', 'Contact support'],
            ['checkCircle', 'Send feedback', "Tell us what's working and what could be better.", 'Send feedback'],
            ['alert', 'Reset demo data', 'Restore this prototype to its original sample data.', 'Reset demo data'],
          ].map(([icon, title, desc, cta]) => (
            <Card className="card-pad" key={title}>
              <span className="stat-icon icon-bg-brand" style={{ marginBottom: 10 }}><Icon name={icon} /></span>
              <div style={{ fontWeight: 700, fontSize: 13.5, marginBottom: 4 }}>{title}</div>
              <p className="muted" style={{ marginBottom: 12 }}>{desc}</p>
              <button className={`btn btn-sm ${cta === 'Reset demo data' ? 'btn-danger' : ''}`} onClick={() => onToast(cta === 'Reset demo data' ? 'Demo data reset.' : 'This is a prototype — no live support channel is connected.')}>{cta}</button>
            </Card>
          ))}
        </div>
      )}
    </>
  );
}

function Profile({ onToast }: { onToast: (text: string) => void }) {
  const usage = [['Expenses logged', expenses.length, 'expenses'], ['Revenues logged', revenues.length, 'revenues'], ['Invoices created', invoices.length, 'invoices'], ['Reports generated', 3, 'reports']] as const;
  return (
    <>
      <div className="grid grid-2" style={{ alignItems: 'start', gap: 16 }}>
        <Card className="card-pad">
          <div className="flex gap-8" style={{ alignItems: 'center', marginBottom: 18 }}>
            <div className="avatar-btn" style={{ width: 52, height: 52, fontSize: 18, cursor: 'default' }}>AR</div>
            <div><div style={{ fontWeight: 700, fontSize: 16 }}>Alex Rivera</div><div className="muted">Owner · Alex Rivera Freelance Studio</div></div>
          </div>
          <div className="form-grid">
            <div className="form-field full"><label>Full name</label><input defaultValue="Alex Rivera" /></div>
            <div className="form-field"><label>Email address</label><input defaultValue="admin@alicazum.com" /></div>
            <div className="form-field"><label>Phone number</label><input defaultValue="+34 611 220 934" /></div>
          </div>
          <div style={{ marginTop: 16 }}><button className="btn btn-primary" onClick={() => onToast('Profile saved.')}>Save profile</button></div>
        </Card>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card className="card-pad">
            <div className="section-title" style={{ marginBottom: 12 }}>Usage this workspace</div>
            <div className="grid grid-2" style={{ gap: 10 }}>
              {usage.map(([label, value, icon]) => (
                <div key={label} style={{ background: 'var(--surface-2)', borderRadius: 8, padding: 12 }}>
                  <div className="stat-icon icon-bg-brand" style={{ width: 26, height: 26, marginBottom: 8 }}><Icon name={icon} /></div>
                  <div style={{ fontSize: 18, fontWeight: 700 }}>{value}</div>
                  <div className="muted">{label}</div>
                </div>
              ))}
            </div>
          </Card>
          <Card className="card-pad">
            <div className="section-title" style={{ marginBottom: 6 }}>Notification preferences</div>
            <p className="muted" style={{ marginBottom: 10 }}>Manage what you get notified about.</p>
            {['Email notifications', 'Overdue invoice alerts', 'Budget alerts'].map((label) => (
              <div className="settings-row" key={label}>
                <div className="lbl" style={{ fontSize: 12.5 }}>{label}</div>
                <label className="toggle"><input type="checkbox" defaultChecked onChange={() => onToast('Preference saved.')} /><span className="track" /><span className="thumb" /></label>
              </div>
            ))}
          </Card>
        </div>
      </div>
      <Card style={{ marginTop: 16 }}>
        <div className="card-head"><h3>Your recent activity</h3></div>
        <div className="card-body">
          <div className="timeline">
            {[
              ['Alex Rivera created invoice INV-2026-1078 for Acme Retail', 'Sep 3, 2026'],
              ['Marta Solis reconciled 6 transactions on Business Checking', 'Sep 2, 2026'],
              ['Alex Rivera logged an expense: Figma subscription (€15.00)', 'Sep 2, 2026'],
            ].map(([t, d]) => (
              <div className="timeline-item" key={t}><span className="timeline-dot" /><div><div className="t">{t}</div><div className="d">{d}</div></div></div>
            ))}
          </div>
        </div>
      </Card>
    </>
  );
}

export default App;
