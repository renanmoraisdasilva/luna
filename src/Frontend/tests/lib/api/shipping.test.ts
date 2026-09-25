import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getShipment, getShipments, getShippingMethods, shippingApi } from '../../../lib/api/shipping';

describe('Shipping API client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns the shipping methods from the service response', async () => {
    const methods = [{ id: 'standard', code: 'STANDARD', name: 'Standard', cost: 5, estimatedDeliveryDays: 3 }];
    const get = vi.spyOn(shippingApi, 'get').mockResolvedValue({ data: { methods } });

    await expect(getShippingMethods()).resolves.toEqual(methods);
    expect(get).toHaveBeenCalledWith('/shipping-methods');
  });

  it('propagates shipping service failures', async () => {
    vi.spyOn(shippingApi, 'get').mockRejectedValue(new Error('Shipping unavailable.'));

    await expect(getShippingMethods()).rejects.toThrow('Shipping unavailable.');
  });

  it('loads paged shipment operations data with filters', async () => {
    const data = { items: [], totalCount: 0, page: 1, pageSize: 25 };
    const get = vi.spyOn(shippingApi, 'get').mockResolvedValue({ data });

    await expect(getShipments({ status: 'InTransit', search: 'LUNA-1', page: 1, pageSize: 25 })).resolves.toEqual(data);
    expect(get).toHaveBeenCalledWith('/shipments', { params: { status: 'InTransit', search: 'LUNA-1', page: 1, pageSize: 25 } });
  });

  it('loads a shipment detail by tracking aggregate ID', async () => {
    const data = { shipmentId: 'shipment-1' };
    const get = vi.spyOn(shippingApi, 'get').mockResolvedValue({ data });

    await expect(getShipment('shipment-1')).resolves.toEqual(data);
    expect(get).toHaveBeenCalledWith('/shipments/shipment-1');
  });
});
