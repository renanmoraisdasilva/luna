/** @type {import('next').NextConfig} */
const serviceSpecs = [
	['identity', process.env.IDENTITY_API_INTERNAL_URL],
	['catalog', process.env.CATALOG_API_INTERNAL_URL],
	['orders', process.env.ORDERS_API_INTERNAL_URL],
	['payments', process.env.PAYMENTS_API_INTERNAL_URL],
	['inventory', process.env.INVENTORY_API_INTERNAL_URL],
	['shipping', process.env.SHIPPING_API_INTERNAL_URL],
];

const nextConfig = {
	output: 'standalone',
	images: {
		remotePatterns: [{ protocol: 'https', hostname: 'images.unsplash.com' }],
	},
	async rewrites() {
		const configuredServices = serviceSpecs.filter(([, baseUrl]) => baseUrl);
		const swaggerRewrites = configuredServices.map(([service, baseUrl]) => ({
			source: `/api/swagger/${service}`,
			destination: `${baseUrl}/swagger/v1/swagger.json`,
		}));
		const serviceHealthRewrites = configuredServices.map(([service, baseUrl]) => ({
			source: `/api/services/${service}/health`,
			destination: `${baseUrl}/health`,
		}));
		const identityBaseUrl = configuredServices.find(([service]) => service === 'identity')?.[1];
		const identityDiscoveryRewrites = identityBaseUrl
			? [
					{
						source: '/api/services/identity/.well-known/openid-configuration',
						destination: `${identityBaseUrl}/.well-known/openid-configuration`,
					},
					{
						source: '/api/services/identity/.well-known/jwks',
						destination: `${identityBaseUrl}/.well-known/jwks`,
					},
				]
			: [];
		const serviceRewrites = configuredServices.map(([service, baseUrl]) => ({
			source: `/api/services/${service}/:path*`,
			destination: `${baseUrl}/api/v1/${service}/:path*`,
		}));

		return [...swaggerRewrites, ...identityDiscoveryRewrites, ...serviceHealthRewrites, ...serviceRewrites];
	},
};
export default nextConfig;
