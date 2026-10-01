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

		// The OpenAPI documents are proxied only when the explorer is explicitly enabled. The services serve
		// them in Development only, so without this gate the rewrite would exist in production and simply
		// return 404 from the upstream service.
		const swaggerEnabled = process.env.ENABLE_API_EXPLORER === 'true';
		const swaggerRewrites = swaggerEnabled
			? configuredServices.map(([service, baseUrl]) => ({
					source: `/api/swagger/${service}`,
					destination: `${baseUrl}/swagger/v1/swagger.json`,
				}))
			: [];
		const serviceHealthRewrites = configuredServices.map(([service, baseUrl]) => ({
			source: `/api/services/${service}/health`,
			destination: `${baseUrl}/health`,
		}));
		const shippingRewrites = configuredServices.some(([service]) => service === 'shipping')
			? [{
					source: '/api/services/shipping/:path*',
					destination: `${configuredServices.find(([service]) => service === 'shipping')?.[1]}/api/v1/:path*`,
				}]
			: [];
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
		const serviceRewrites = configuredServices.filter(([service]) => service !== 'shipping').map(([service, baseUrl]) => ({
			source: `/api/services/${service}/:path*`,
			destination: `${baseUrl}/api/v1/${service}/:path*`,
		}));

		return [...swaggerRewrites, ...identityDiscoveryRewrites, ...serviceHealthRewrites, ...shippingRewrites, ...serviceRewrites];
	},
};
export default nextConfig;
