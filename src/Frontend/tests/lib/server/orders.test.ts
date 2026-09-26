import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ getAccessToken: vi.fn() }));

vi.mock('../../../lib/auth-server', () => ({ getAccessToken: mocks.getAccessToken }));

import { getOrderById, getOrderProducts, getOrderSummaries, getShipmentTracking } from '../../../lib/server/orders';

function response(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('server order helpers', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    mocks.getAccessToken.mockResolvedValue('access-token');
    vi.stubEnv('ORDERS_API_INTERNAL_URL', 'http://orders');
    vi.stubEnv('CATALOG_API_INTERNAL_URL', 'http://catalog');
    vi.stubEnv('SHIPPING_API_INTERNAL_URL', 'http://shipping');
    vi.stubGlobal('fetch', vi.fn());
  });

  it('loads order summaries with the service authorization header', async () => {
    vi.mocked(fetch).mockResolvedValue(response(200, [{ id: 'order-1' }]));

    await expect(getOrderSummaries()).resolves.toEqual([{ id: 'order-1' }]);
    expect(fetch).toHaveBeenCalledWith('http://orders/api/v1/orders', expect.objectContaining({
      headers: { Authorization: 'Bearer access-token', Accept: 'application/json' },
      cache: 'no-store',
    }));
  });

  it('rejects when the order summary service fails or is not configured', async () => {
    vi.mocked(fetch).mockResolvedValue(response(503, { message: 'down' }));
    await expect(getOrderSummaries()).rejects.toThrow('Unable to load orders.');

    mocks.getAccessToken.mockResolvedValue(null);
    await expect(getOrderSummaries()).rejects.toThrow('Orders service is not configured.');
  });

  it('loads an order, handles not found, and rejects other failures', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(response(200, { id: 'order/1' }))
      .mockResolvedValueOnce(response(404, {}))
      .mockResolvedValueOnce(response(500, {}));

    await expect(getOrderById('order/1')).resolves.toEqual({ id: 'order/1' });
    await expect(getOrderById('missing')).resolves.toBeNull();
    await expect(getOrderById('broken')).rejects.toThrow('Unable to load the order.');
    expect(fetch).toHaveBeenNthCalledWith(1, 'http://orders/api/v1/orders/order%2F1', expect.any(Object));
  });

  it('loads customer-scoped shipment tracking and handles missing shipments', async () => {
    const tracking = { shipmentId: 'shipment-1', status: 'InTransit', trackingNumber: 'LUNA-TRACK-1' };
    vi.mocked(fetch)
      .mockResolvedValueOnce(response(200, tracking))
      .mockResolvedValueOnce(response(404, {}));

    await expect(getShipmentTracking('shipment/1')).resolves.toEqual(tracking);
    await expect(getShipmentTracking('missing')).resolves.toBeNull();
    expect(fetch).toHaveBeenNthCalledWith(1, 'http://shipping/api/v1/shipments/shipment%2F1/tracking', expect.objectContaining({
      headers: { Authorization: 'Bearer access-token', Accept: 'application/json' },
      cache: 'no-store',
    }));
  });

  it('rejects when shipment tracking is unavailable or not configured', async () => {
    vi.mocked(fetch).mockResolvedValue(response(503, {}));
    await expect(getShipmentTracking('shipment-1')).rejects.toThrow('Unable to load shipment tracking.');

    vi.stubEnv('SHIPPING_API_INTERNAL_URL', '');
    await expect(getShipmentTracking('shipment-1')).rejects.toThrow('Shipping service is not configured.');
  });

  it('returns only catalog products that load successfully', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(response(200, { id: 'product-1', name: 'Keyboard' }))
      .mockResolvedValueOnce(response(404, {}));

    const order = { items: [{ productId: 'product-1' }, { productId: 'product-2' }] } as never;
    await expect(getOrderProducts(order)).resolves.toEqual({ 'product-1': { id: 'product-1', name: 'Keyboard' } });
  });

  it('returns no products when authentication or catalog configuration is unavailable', async () => {
    const order = { items: [{ productId: 'product-1' }] } as never;

    mocks.getAccessToken.mockResolvedValue(null);
    await expect(getOrderProducts(order)).resolves.toEqual({});

    mocks.getAccessToken.mockResolvedValue('access-token');
    vi.stubEnv('CATALOG_API_INTERNAL_URL', '');
    await expect(getOrderProducts(order)).resolves.toEqual({});
  });
});
