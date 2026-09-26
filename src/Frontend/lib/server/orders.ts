import { getAccessToken } from '../auth-server';
import type { CatalogProduct } from '../../types/catalog';
import type { FulfillmentOrder, Order, OrderSummary } from '../api/orders';
import type { ShipmentTracking } from '../api/shipping';

function getHeaders(accessToken: string) {
  return { Authorization: `Bearer ${accessToken}`, Accept: 'application/json' };
}

export async function getOrderSummaries(): Promise<OrderSummary[]> {
  const accessToken = await getAccessToken();
  const ordersUrl = process.env.ORDERS_API_INTERNAL_URL;
  if (!accessToken || !ordersUrl) {
    throw new Error('Orders service is not configured.');
  }

  const response = await fetch(`${ordersUrl}/api/v1/orders`, {
    headers: getHeaders(accessToken),
    cache: 'no-store',
  });
  if (!response.ok) {
    throw new Error('Unable to load orders.');
  }
  return response.json() as Promise<OrderSummary[]>;
}

export async function getOrderById(orderId: string): Promise<Order | null> {
  const accessToken = await getAccessToken();
  const ordersUrl = process.env.ORDERS_API_INTERNAL_URL;
  if (!accessToken || !ordersUrl) {
    throw new Error('Orders service is not configured.');
  }

  const response = await fetch(`${ordersUrl}/api/v1/orders/${encodeURIComponent(orderId)}`, {
    headers: getHeaders(accessToken),
    cache: 'no-store',
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error('Unable to load the order.');
  }
  return response.json() as Promise<Order>;
}

export async function getFulfillmentOrderById(orderId: string): Promise<FulfillmentOrder | null> {
  const accessToken = await getAccessToken();
  const ordersUrl = process.env.ORDERS_API_INTERNAL_URL;
  if (!accessToken || !ordersUrl) {
    throw new Error('Orders service is not configured.');
  }

  const response = await fetch(`${ordersUrl}/api/v1/orders/fulfillment/${encodeURIComponent(orderId)}`, {
    headers: getHeaders(accessToken),
    cache: 'no-store',
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error('Unable to load the fulfillment order.');
  }
  return response.json() as Promise<FulfillmentOrder>;
}

export async function getShipmentTracking(shipmentId: string): Promise<ShipmentTracking | null> {
  const accessToken = await getAccessToken();
  const shippingUrl = process.env.SHIPPING_API_INTERNAL_URL;
  if (!accessToken || !shippingUrl) {
    throw new Error('Shipping service is not configured.');
  }

  const response = await fetch(`${shippingUrl}/api/v1/shipments/${encodeURIComponent(shipmentId)}/tracking`, {
    headers: getHeaders(accessToken),
    cache: 'no-store',
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error('Unable to load shipment tracking.');
  }
  return response.json() as Promise<ShipmentTracking>;
}

export async function getOrderProducts(order: Order): Promise<Record<string, CatalogProduct>> {
  const accessToken = await getAccessToken();
  const catalogUrl = process.env.CATALOG_API_INTERNAL_URL;
  if (!accessToken || !catalogUrl) {
    return {};
  }

  const entries = await Promise.all(order.items.map(async (item) => {
    const response = await fetch(`${catalogUrl}/api/v1/catalog/products/${item.productId}`, {
      headers: getHeaders(accessToken),
      cache: 'no-store',
    });
    return response.ok ? [item.productId, await response.json() as CatalogProduct] as const : null;
  }));
  return Object.fromEntries(entries.filter((entry): entry is [string, CatalogProduct] => entry !== null));
}
