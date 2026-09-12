import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from './features/auth/pages/login-page';
import { ProtectedRoute } from './features/auth/components/protected-route';
import { HomePage } from './features/home/pages/home-page';
import { FinancialOverviewPage } from './features/accounts/pages/financial-overview-page';
import { InvoicesPage } from './features/invoices/pages/invoices-page';
import { InvoiceDetailPage } from './features/invoices/pages/invoice-detail-page';
import { EditInvoicePage } from './features/invoices/pages/edit-invoice-page';
import { PaymentsPage } from './features/payments/pages/payments-page';
import { SettingsPage } from './features/settings/pages/settings-page';
import { TransactionsPage } from './features/transactions/pages/transactions-page';
import { UserPage } from './features/user/pages/user-page';
import { AppShell } from './shared/components/app-shell';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/financial-overview" element={<FinancialOverviewPage />} />
          <Route path="/invoices" element={<InvoicesPage />} />
            <Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} />
            <Route path="/invoices/:invoiceId/edit" element={<EditInvoicePage />} />
          <Route path="/payments" element={<PaymentsPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/user" element={<UserPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
