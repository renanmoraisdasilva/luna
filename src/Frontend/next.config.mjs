/** @type {import('next').NextConfig} */
const serviceSpecs = [
	['identity', process.env.IDENTITY_API_INTERNAL_URL ?? 'http://localhost:5001'],
	['catalog', process.env.CATALOG_API_INTERNAL_URL ?? 'http://localhost:5002'],
	['orders', process.env.ORDERS_API_INTERNAL_URL ?? 'http://localhost:5003'],
	['payments', process.env.PAYMENTS_API_INTERNAL_URL ?? 'http://localhost:5004'],
	['inventory', process.env.INVENTORY_API_INTERNAL_URL ?? 'http://localhost:5005'],
	['shipping', process.env.SHIPPING_API_INTERNAL_URL ?? 'http://localhost:5006'],
];

const nextConfig = {
	output: 'standalone',
	async rewrites() {
		const swaggerRewrites = serviceSpecs.map(([service, baseUrl]) => ({
			source: `/api/swagger/${service}`,
			destination: `${baseUrl}/swagger/v1/swagger.json`,
		}));
		const serviceRewrites = serviceSpecs.map(([service, baseUrl]) => ({
			source: `/api/services/${service}/:path*`,
			destination: `${baseUrl}/api/v1/${service}/:path*`,
		}));

		return [...swaggerRewrites, ...serviceRewrites];
	},
};
export default nextConfig;
