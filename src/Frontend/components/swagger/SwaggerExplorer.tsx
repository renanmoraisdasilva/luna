'use client';

import { useState } from 'react';
import SwaggerUI from 'swagger-ui-react';
import 'swagger-ui-react/swagger-ui.css';
import { useSwaggerQuery } from '../../lib/queries/swagger';

const specs = [
  { url: '/api/swagger/identity', name: 'Identity API' },
  { url: '/api/swagger/catalog', name: 'Catalog API' },
  { url: '/api/swagger/orders', name: 'Orders API' },
  { url: '/api/swagger/payments', name: 'Payments API' },
  { url: '/api/swagger/inventory', name: 'Inventory API' },
  { url: '/api/swagger/shipping', name: 'Shipping API' },
];

const SwaggerUIComponent: any = SwaggerUI;

export default function SwaggerExplorer() {
  const [selectedSpec, setSelectedSpec] = useState(specs[0].url);
  const { data: resolvedSpec, isLoading, isError } = useSwaggerQuery(selectedSpec);

  return (
    <>
      <label className="swagger-selector">
        Service API
        <select value={selectedSpec} onChange={(event) => setSelectedSpec(event.target.value)}>
          {specs.map((spec) => <option key={spec.url} value={spec.url}>{spec.name}</option>)}
        </select>
      </label>

      {isLoading && (
        <div className="swagger-state swagger-state-loading" role="status">
          <span className="swagger-state-spinner" aria-hidden="true" />
          <span>Loading API definition</span>
        </div>
      )}
      {isError && (
        <div className="swagger-state swagger-state-error" role="alert">
          <span className="material-symbols-outlined" aria-hidden="true">error</span>
          <span>Unable to load API definition</span>
        </div>
      )}
      {!isLoading && !isError && resolvedSpec && (
        <SwaggerUIComponent key={selectedSpec} spec={resolvedSpec} deepLinking displayRequestDuration />
      )}
    </>
  );
}
