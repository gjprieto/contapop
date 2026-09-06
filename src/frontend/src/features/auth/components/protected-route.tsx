import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { ApiError } from '../../../shared/api/client';
import { useCurrentUser } from '../api/auth-queries';

export function ProtectedRoute() {
  const location = useLocation();
  const currentUser = useCurrentUser();

  if (currentUser.isPending) {
    return <main className="auth-status" role="status">Checking your session...</main>;
  }

  if (currentUser.error instanceof ApiError && currentUser.error.status === 401) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (currentUser.isError) {
    return (
      <main className="auth-status" role="alert">
        We could not verify your session. Refresh the page and try again.
      </main>
    );
  }

  return <Outlet />;
}
