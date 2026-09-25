import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ShipmentQueue from '../../../components/operations/ShipmentQueue';
import { markShipmentDelivered, markShipmentInTransit } from '../../../lib/api/orders';
import { getShipments } from '../../../lib/api/shipping';

vi.mock('../../../lib/api/orders', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api/orders')>('../../../lib/api/orders');
  return { ...actual, markShipmentDelivered: vi.fn(), markShipmentInTransit: vi.fn() };
});
vi.mock('../../../lib/api/shipping', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api/shipping')>('../../../lib/api/shipping');
  return { ...actual, getShipments: vi.fn() };
});

const shipments = [
  {
    shipmentId: 'shipment-1',
    orderId: 'order-1',
    customerId: 'customer-1',
    customerName: 'Jane Doe',
    destination: 'Austin, TX',
    trackingNumber: 'LUNA-TRACK-1',
    status: 'Created' as const,
    createdAt: '2026-09-25T10:00:00Z',
    inTransitAt: null,
    deliveredAt: null,
    availableAction: 'MarkInTransit' as const,
  },
];

function renderQueue() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><ShipmentQueue /></QueryClientProvider>);
}

describe('ShipmentQueue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(markShipmentInTransit).mockResolvedValue({ orderId: 'order-1', orderStatus: 'Shipped' });
    vi.mocked(markShipmentDelivered).mockResolvedValue({ orderId: 'order-1', orderStatus: 'Delivered' });
    vi.mocked(getShipments).mockResolvedValue({ items: shipments, totalCount: 1, page: 1, pageSize: 5 });
  });

  it('loads shipments and sends the backend-provided in-transit command', async () => {
    renderQueue();

    expect(await screen.findByText('LUNA-TRACK-1')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Mark In Transit' }));

    await waitFor(() => expect(markShipmentInTransit).toHaveBeenCalledWith('shipment-1', expect.anything()));
  });
});