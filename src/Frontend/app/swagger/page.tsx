import SwaggerExplorer from '../../components/swagger/SwaggerExplorer';

export const metadata = {
  title: 'Luna API Explorer',
  description: 'Unified OpenAPI documentation for Luna services',
};

export default function SwaggerPage() {
  return (
    <main className="swagger-page">
      <SwaggerExplorer />
    </main>
  );
}
