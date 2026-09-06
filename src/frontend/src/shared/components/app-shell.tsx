import { Outlet } from 'react-router-dom';

export function AppShell() {
  return (
    <div className="app-shell">
      <header className="app-shell-header">
        <a className="app-shell-brand" href="/">Contapop</a>
      </header>
      <Outlet />
    </div>
  );
}
