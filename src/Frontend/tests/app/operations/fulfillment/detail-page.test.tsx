import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FulfillmentOrderDetailsPage from '../../../../app/operations/fulfillment/[orderId]/page';
import { getCurrentUserServer } from '../../../../lib/auth-server';
import { getFulfillmentOrderById } from '../../../../lib/server/orders';

const mocks = vi.hoisted(() => ({ redirect: vi.fn(), notFound: vi.fn() }));

vi.mock('next/navigation', () => ({ redirect: mocks.redirect, notFound: mocks.notFound }));
vi.mock('../../../../lib/auth-server', () => ({ getCurrentUserServer: vi.fn() }));
vi.mock('../../../../lib/server/orders', () => ({ getFulfillmentOrderById: vi.fn() }));
vi.mock('../../../../components/operations/FulfillmentOrderDetails', () => ({ default: () => <div>Fulfillment order details</div> }));

describe('FulfillmentOrderDetailsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getFulfillmentOrderById).mockResolvedValue({
      orderId: 'order-1',
      customerId: 'customer-1',
      customerName: 'Jane Doe',
      orderStatus: 'Confirmed',
      paymentStatus: 'Authorized',
      subtotal: 100,
      shippingCost: 0,
      total: 100,
      shippingMethodCode: 'standard',
      createdAt: '2026-09-25T10:42:00Z',
      shippingAddress: { fullName: 'Jane Doe', addressLine1: '1 Main Street', city: 'Austin', stateOrProvince: 'TX', postalCode: '78701', country: 'US' },
      items: [],
      availableAction: 'StartPreparing',
    });
  });

  it('redirects non-admin users away from operations', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue({ id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe', roles: ['Customer'] });

    await FulfillmentOrderDetailsPage({ params: Promise.resolve({ orderId: 'order-1' }) });

    expect(mocks.redirect).toHaveBeenCalledWith('/shop');
  });

  it('loads the detail read model for admins', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue({ id: 'admin-1', email: 'admin@example.com', firstName: 'Admin', lastName: 'User', roles: ['Admin'] });

    const page = await FulfillmentOrderDetailsPage({ params: Promise.resolve({ orderId: 'order-1' }) });
    render(page);

    expect(getFulfillmentOrderById).toHaveBeenCalledWith('order-1');
    expect(mocks.redirect).not.toHaveBeenCalled();
  });
});