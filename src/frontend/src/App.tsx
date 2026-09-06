import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from './features/auth/pages/login-page';
import { ProtectedRoute } from './features/auth/components/protected-route';
import { HomePage } from './features/home/pages/home-page';
import { SettingsPage } from './features/settings/pages/settings-page';
import { UserPage } from './features/user/pages/user-page';
import { AppShell } from './shared/components/app-shell';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<AppShell />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/user" element={<UserPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
