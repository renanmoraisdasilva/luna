'use client';

import { useEffect, useState } from 'react';
import SwaggerUI from 'swagger-ui-react';
import 'swagger-ui-react/swagger-ui.css';

const specs = [
  { url: '/api/swagger/identity', name: 'Identity API' },
  { url: '/api/swagger/catalog', name: 'Catalog API' },
  { url: '/api/swagger/orders', name: 'Orders API' },
  { url: '/api/swagger/payments', name: 'Payments API' },
  { url: '/api/swagger/inventory', name: 'Inventory API' },
  { url: '/api/swagger/shipping', name: 'Shipping API' },
];

const SwaggerUIComponent: any = SwaggerUI;

function rewriteGatewaySpec(serviceName: string, rawSpec: Record<string, any>) {
  const serviceBase = `/api/v1/${serviceName}`;
  const rewritten: any = {
    ...rawSpec,
    servers: [{ url: `/api/services/${serviceName}` }],
    paths: {},
  };

  const rawPaths = (rawSpec as any).paths ?? {};

  for (const [route, definition] of Object.entries(rawPaths) as [string, any][]) {
    const rewrittenRoute = route.startsWith(serviceBase)
      ? route.slice(serviceBase.length) || '/'
      : route;

    rewritten.paths[rewrittenRoute] = definition;
  }

  return rewritten;
}

export default function SwaggerExplorer() {
  const [selectedSpec, setSelectedSpec] = useState(specs[0].url);
  const [resolvedSpec, setResolvedSpec] = useState<Record<string, any> | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const serviceName = selectedSpec.split('/').at(-1) ?? 'identity';
    setLoading(true);

    fetch(selectedSpec)
      .then((response) => response.json())
      .then((data) => {
        setResolvedSpec(rewriteGatewaySpec(serviceName, data));
      })
      .catch(() => setResolvedSpec(null))
      .finally(() => setLoading(false));
  }, [selectedSpec]);

  return (
    <>
      <label className="swagger-selector">
        Service API
        <select value={selectedSpec} onChange={(event) => setSelectedSpec(event.target.value)}>
          {specs.map((spec) => <option key={spec.url} value={spec.url}>{spec.name}</option>)}
        </select>
      </label>

      {loading && <div>Loading API definition…</div>}
      {!loading && resolvedSpec && (
        <SwaggerUIComponent key={selectedSpec} spec={resolvedSpec} deepLinking displayRequestDuration />
      )}
    </>
  );
}