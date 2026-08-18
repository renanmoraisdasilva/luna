import Link from 'next/link';
import SwaggerExplorer from './SwaggerExplorer';

export const metadata = {
  title: 'Luna API Explorer',
  description: 'Unified OpenAPI documentation for Luna services',
};

export default function SwaggerPage() {
  return (
    <main className="swagger-page">
      <header className="swagger-header">
        <Link href="/">LUNA</Link>
        <span>Unified API Explorer</span>
        <Link className="swagger-back" href="/">Back to storefront</Link>
      </header>
      <SwaggerExplorer />
    </main>
  );
}