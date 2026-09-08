import { useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { useCurrentUser } from '../../features/auth/api/auth-queries';

type NavigationItem = {
  icon: keyof typeof icons;
  label: string;
  to?: string;
};

const icons = {
  home: <path d="M3 11l9-7 9 7M5 10v10h14V10M9 20v-6h6v6" />,
  overview: <path d="M3 3v18h18M7 15l4-5 3 3 5-7" />,
  expenses: <><circle cx="12" cy="12" r="9" /><path d="M8 12h8" /></>,
  revenues: <><circle cx="12" cy="12" r="9" /><path d="M12 8v8M8 12h8" /></>,
  invoices: <><path d="M6 3h9l5 5v13H6zM14 3v5h5M9 13h6M9 17h6M9 9h2" /></>,
  payments: <><rect x="2.5" y="5.5" width="19" height="14" rx="2" /><path d="M2.5 10h19M6 15h4" /></>,
  transactions: <path d="M7 4v13a2 2 0 002 2h9M17 20l3-3-3-3M17 20V7a2 2 0 00-2-2H6M7 4L4 7l3 3" />,
  reports: <><rect x="3" y="3" width="18" height="18" rx="2" /><path d="M8 16v-4M12 16V8M16 16v-7" /></>,
  plans: <><path d="M12 2v4" /><circle cx="12" cy="12" r="9" /><circle cx="12" cy="12" r="5" /><circle cx="12" cy="12" r="1" /></>,
  settings: <><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 00.34 1.87l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.7 1.7 0 00-1.87-.34 1.7 1.7 0 00-1.04 1.56V21a2 2 0 11-4 0v-.09A1.7 1.7 0 009 19.4a1.7 1.7 0 00-1.87.34l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.7 1.7 0 004.6 15a1.7 1.7 0 00-1.56-1.04H3a2 2 0 110-4h.09A1.7 1.7 0 004.6 9a1.7 1.7 0 00-.34-1.87l-.06-.06a2 2 0 112.83-2.83l.06.06A1.7 1.7 0 009 4.6a1.7 1.7 0 001.04-1.56V3a2 2 0 114 0v.09c0 .68.4 1.29 1.04 1.56.66.27 1.4.14 1.87-.34l.06-.06a2 2 0 112.83 2.83l-.06.06c-.48.47-.6 1.2-.34 1.87.27.64.88 1.04 1.56 1.04H21a2 2 0 110 4h-.09c-.68 0-1.29.4-1.51 1.04z" /></>,
  user: <><circle cx="12" cy="8" r="4" /><path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7" /></>,
  search: <><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" /></>,
  bell: <><path d="M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9" /><path d="M13.7 21a2 2 0 01-3.4 0" /></>,
  menu: <path d="M3 6h18M3 12h18M3 18h18" />,
};

const navigation: Array<NavigationItem | string> = [
  { icon: 'home', label: 'Home', to: '/' },
  { icon: 'overview', label: 'Financial Overview', to: '/financial-overview' },
  'Money',
  { icon: 'expenses', label: 'Expenses' },
  { icon: 'revenues', label: 'Revenues' },
  { icon: 'invoices', label: 'Invoices' },
  { icon: 'payments', label: 'Payments' },
  { icon: 'transactions', label: 'Transactions', to: '/transactions' },
  'Plan & analyze',
  { icon: 'reports', label: 'Reports' },
  { icon: 'plans', label: 'Plans' },
  'Account',
  { icon: 'settings', label: 'Settings', to: '/settings' },
  { icon: 'user', label: 'My Profile', to: '/user' },
];

const pageTitles: Record<string, [string, string]> = {
  '/': ['Home', 'Your financial snapshot at a glance'],
  '/financial-overview': ['Financial Overview', 'Manage accounts and payment cards'],
  '/transactions': ['Transactions', 'Review and record your bank-side movements'],
  '/settings': ['Settings', 'Manage your account preferences'],
  '/user': ['My Profile', 'Your personal information'],
};

function Icon({ name }: { name: keyof typeof icons }) {
  return <svg className="icon" viewBox="0 0 24 24" aria-hidden="true">{icons[name]}</svg>;
}

export function AppShell() {
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const location = useLocation();
  const currentUser = useCurrentUser();
  const [title, subtitle] = pageTitles[location.pathname] ?? pageTitles['/'];
  const initial = currentUser.data?.name.trim().charAt(0).toUpperCase() ?? 'C';

  return (
    <div className={`app ${mobileNavOpen ? 'nav-open' : ''}`}>
      <aside className="sidebar">
        <Link className="sidebar-brand" to="/" onClick={() => setMobileNavOpen(false)}>
          <span className="brand-mark">C</span><span className="brand-name">Contapop</span>
        </Link>
        <nav className="sidebar-nav" aria-label="Main navigation">
          {navigation.map((item) => {
            if (typeof item === 'string') return <p className="nav-section-label" key={item}>{item}</p>;
            if (!item.to) return <span className="nav-item nav-item-disabled" key={item.label}><Icon name={item.icon} /><span>{item.label}</span></span>;
            return <NavLink className="nav-item" key={item.label} to={item.to} end={item.to === '/'} onClick={() => setMobileNavOpen(false)}><Icon name={item.icon} /><span>{item.label}</span></NavLink>;
          })}
        </nav>
        <div className="sidebar-footer"><div className="sidebar-tenant"><span className="tenant-mark">{initial}</span><div className="tenant-meta"><p className="name">{currentUser.data?.name ?? 'Your workspace'}</p><p className="role">Owner</p></div></div></div>
      </aside>
      {mobileNavOpen && <button className="sidebar-overlay" type="button" aria-label="Close menu" onClick={() => setMobileNavOpen(false)} />}
      <div className="main">
        <header className="topbar">
          <button className="icon-btn only-mobile" type="button" aria-label="Open menu" onClick={() => setMobileNavOpen(true)}><Icon name="menu" /></button>
          <div className="topbar-title"><h1>{title}</h1><p className="muted">{subtitle}</p></div><div className="topbar-spacer" />
          <label className="topbar-search"><Icon name="search" /><input type="search" placeholder="Search everything..." disabled /></label>
          <button className="icon-btn" type="button" aria-label="Notifications" disabled><Icon name="bell" /></button>
          <NavLink className="avatar-btn" aria-label="Open profile" to="/user">{initial}</NavLink>
        </header>
        <main className="content"><Outlet /></main>
      </div>
    </div>
  );
}
