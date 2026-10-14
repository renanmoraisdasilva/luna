import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Order } from '../../../lib/api/orders';
import type { CatalogProduct } from '../../../types/catalog';

const mocks = vi.hoisted(() => ({
  getCurrentUserServer: vi.fn(),
  getOrderById: vi.fn(),
  getOrderProducts: vi.fn(),
  getShipmentTracking: vi.fn(),
}));

vi.mock('../../../lib/auth-server', () => ({
  getCurrentUserServer: mocks.getCurrentUserServer,
}));

vi.mock('../../../lib/server/orders', () => ({
  getOrderById: mocks.getOrderById,
  getOrderProducts: mocks.getOrderProducts,
  getShipmentTracking: mocks.getShipmentTracking,
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/orders/order-1',
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  notFound: () => {
    throw new Error('notFound called unexpectedly');
  },
  redirect: () => {
    throw new Error('redirect called unexpectedly');
  },
}));

import OrderDetailsPage from '../../../app/(store)/orders/[orderId]/page';

const product: CatalogProduct = {
  id: 'product-1',
  sku: 'KB-001',
  name: 'Wireless Mechanical Keyboard',
  description: 'A keyboard.',
  currentPrice: 129,
  categoryId: 'electronics-1',
  categoryName: 'Electronics',
  categorySlug: 'electronics',
  images: [],
};

function orderFixture(overrides: Partial<Order> = {}): Order {
  return {
    id: 'order-1',
    customerId: 'customer-1',
    status: 'Confirmed',
    shipmentId: null,
    subtotal: 129,
    shippingCost: 10,
    total: 139,
    shippingMethodCode: 'STANDARD',
    createdAt: '2026-10-01T12:00:00Z',
    shippingAddress: {
      fullName: 'Ada Lovelace',
      addressLine1: '1 Main Street',
      city: 'Springfield',
      stateOrProvince: 'IL',
      postalCode: '62701',
      country: 'US',
    },
    items: [
      { productId: 'product-1', sku: 'KB-001', productName: 'Wireless Mechanical Keyboard', unitPrice: 129, quantity: 1, lineTotal: 129 },
    ],
    ...overrides,
  };
}

async function renderOrderPage(order: Order) {
  mocks.getCurrentUserServer.mockResolvedValue({ email: 'ada@example.com', roles: ['customer'] });
  mocks.getOrderById.mockResolvedValue(order);
  mocks.getOrderProducts.mockResolvedValue({ 'product-1': product });
  mocks.getShipmentTracking.mockResolvedValue(null);

  const page = await OrderDetailsPage({ params: Promise.resolve({ orderId: order.id }) });
  return render(<QueryClientProvider client={new QueryClient()}>{page}</QueryClientProvider>);
}

describe('OrderDetailsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the full five-stage lifecycle from the specification', async () => {
    await renderOrderPage(orderFixture());

    expect(screen.getByText('Order placed')).toBeInTheDocument();
    expect(screen.getByText('Payment authorized')).toBeInTheDocument();
    expect(screen.getByText('Preparing')).toBeInTheDocument();
    expect(screen.getByText('Shipped')).toBeInTheDocument();
    expect(screen.getByText('Delivered')).toBeInTheDocument();
  });

  it('shows the tracking placeholder until a shipment exists', async () => {
    await renderOrderPage(orderFixture());

    expect(screen.getByText('Shipment tracking will appear here once your order has been dispatched.')).toBeInTheDocument();
    expect(mocks.getShipmentTracking).not.toHaveBeenCalled();
  });
});
