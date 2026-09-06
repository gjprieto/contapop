import { useCurrentUser } from '../../auth/api/auth-queries';

export function HomePage() {
  const currentUser = useCurrentUser();

  if (currentUser.isPending) {
    return <main className="page" aria-busy="true"><p role="status">Loading your workspace...</p></main>;
  }

  if (currentUser.isError) {
    return (
      <main className="page">
        <p role="alert">We could not load your workspace.</p>
        <button type="button" className="btn" onClick={() => void currentUser.refetch()}>Try again</button>
      </main>
    );
  }

  return (
    <main className="page home-page">
      <p className="eyebrow">Your workspace</p>
      <h1>Hello, {currentUser.data.name}</h1>
      <p className="page-intro">Your financial overview will appear here as you start recording activity.</p>
    </main>
  );
}
