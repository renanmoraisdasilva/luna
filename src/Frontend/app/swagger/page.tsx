import { notFound, redirect } from 'next/navigation';
import SwaggerExplorer from '../../components/swagger/SwaggerExplorer';
import { getCurrentUserServer } from '../../lib/auth-server';

export const metadata = {
  title: 'Luna API Explorer',
  description: 'Unified OpenAPI documentation for Luna services',
};

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
