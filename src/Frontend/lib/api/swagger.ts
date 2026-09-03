import axios from 'axios';

export function rewriteGatewaySpec(
  serviceName: string,
  rawSpec: Record<string, any>,
) {
  const serviceBase = `/api/v1/${serviceName}`;
  const rewritten: Record<string, any> = {
    ...rawSpec,
    servers: [{ url: `/api/services/${serviceName}` }],
    paths: {},
  };

  const rawPaths = rawSpec.paths ?? {};

  for (const [route, definition] of Object.entries(rawPaths)) {
    const rewrittenRoute = route.startsWith(serviceBase)
      ? route.slice(serviceBase.length) || '/'
      : route;

    rewritten.paths[rewrittenRoute] = definition;
  }

  return rewritten;
}

export async function getSwaggerSpec(selectedSpec: string): Promise<Record<string, any>> {
  const response = await axios.get<Record<string, any>>(selectedSpec);
  const serviceName = selectedSpec.split('/').at(-1) ?? 'identity';

  return rewriteGatewaySpec(serviceName, response.data);
}
