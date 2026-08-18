'use client';

import { useState } from 'react';
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

export default function SwaggerExplorer() {
  const [selectedSpec, setSelectedSpec] = useState(specs[0].url);

  return (
    <>
      <label className="swagger-selector">
        Service API
        <select value={selectedSpec} onChange={(event) => setSelectedSpec(event.target.value)}>
          {specs.map((spec) => <option key={spec.url} value={spec.url}>{spec.name}</option>)}
        </select>
      </label>
      <SwaggerUI key={selectedSpec} url={selectedSpec} deepLinking displayRequestDuration />
    </>
  );
}