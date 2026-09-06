import { Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { LoginPage } from './features/auth/pages/login-page';
import { ProtectedRoute } from './features/auth/components/protected-route';
import { AppShell } from './shared/components/app-shell';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<EmptyHome />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function EmptyHome() {
  return (
    <main className="empty-home">
      <p className="eyebrow">Contapop</p>
      <h1>Your workspace is ready.</h1>
      <p>The first account screens will appear here soon.</p>
      <Outlet />
    </main>
  );
}
