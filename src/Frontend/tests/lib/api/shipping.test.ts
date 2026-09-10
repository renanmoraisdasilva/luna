import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getShippingMethods, shippingApi } from '../../../lib/api/shipping';

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
});
