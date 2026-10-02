import { notFound, redirect } from 'next/navigation';
import SwaggerExplorer from '../../components/swagger/SwaggerExplorer';
import { getCurrentUserServer } from '../../lib/auth-server';

export const metadata = {
  title: 'Luna API Explorer',
  description: 'Unified OpenAPI documentation for Luna services',
};

// The explorer aggregates every service's OpenAPI document, including the scopes the internal services
// require. It is disabled unless ENABLE_API_EXPLORER is set, and restricted to administrators even when it
// is enabled.
export default async function SwaggerPage() {
  if (process.env.ENABLE_API_EXPLORER !== 'true') {
    notFound();
  }

  const currentUser = await getCurrentUserServer();
  if (!currentUser) {
    redirect('/login?returnUrl=/swagger');
  }

  if (!currentUser.roles?.some((role) => role.toLowerCase() === 'admin')) {
    redirect('/shop');
  }

  return (
    <main className="swagger-page">
      <SwaggerExplorer />
    </main>
  );
}
