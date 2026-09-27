import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ShipmentQueue from '../../../components/operations/ShipmentQueue';
import { markShipmentDelivered, markShipmentInTransit } from '../../../lib/api/orders';
import { getShipments, type ShipmentListParams, type ShipmentSummary } from '../../../lib/api/shipping';

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

function mockShipments(items: ShipmentSummary[]) {
  vi.mocked(getShipments).mockImplementation(async (params: ShipmentListParams) => {
    const filtered = items.filter((shipment) => {
      const matchesStatus = !params.status || shipment.status === params.status;
      const search = params.search?.toLowerCase() ?? '';
      const matchesSearch = !search || shipment.trackingNumber.toLowerCase().includes(search) || shipment.orderId.toLowerCase().includes(search) || shipment.customerName.toLowerCase().includes(search);
      return matchesStatus && matchesSearch;
    });

    const start = (params.page - 1) * params.pageSize;
    return { items: filtered.slice(start, start + params.pageSize), totalCount: filtered.length, page: params.page, pageSize: params.pageSize };
  });
}

function shipment(overrides: Partial<ShipmentSummary> & { shipmentId: string }): ShipmentSummary {
  return {
    orderId: 'order-1',
    customerId: 'customer-1',
    customerName: 'Jane Doe',
    destination: 'Austin, TX',
    trackingNumber: `LUNA-TRACK-${overrides.shipmentId}`,
    status: 'Created',
    createdAt: '2026-09-25T10:00:00Z',
    inTransitAt: null,
    deliveredAt: null,
    availableAction: 'MarkInTransit',
    ...overrides,
  };
}

const expandedShipments: ShipmentSummary[] = [
  shipment({ shipmentId: 's1', status: 'Created', availableAction: 'MarkInTransit' }),
  shipment({ shipmentId: 's2', status: 'InTransit', availableAction: 'MarkDelivered' }),
  shipment({ shipmentId: 's3', status: 'Delivered', availableAction: 'None' }),
  shipment({ shipmentId: 's4', status: 'Created', availableAction: 'MarkInTransit' }),
  shipment({ shipmentId: 's5', status: 'InTransit', availableAction: 'MarkDelivered' }),
  shipment({ shipmentId: 's6', status: 'Created', availableAction: 'MarkInTransit' }),
  shipment({ shipmentId: 's7', status: 'Delivered', availableAction: 'None' }),
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

  it('renders every shipment status and paginates the queue', async () => {
    mockShipments(expandedShipments);
    renderQueue();

    expect(await screen.findByText('LUNA-TRACK-s1')).toBeInTheDocument();
    expect(screen.getAllByText('In Transit').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Delivered').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Mark Delivered').length).toBeGreaterThan(0);
    expect(screen.getAllByText('-')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Next page' })).not.toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('LUNA-TRACK-s7')).toBeInTheDocument();
    expect(screen.queryByText('LUNA-TRACK-s1')).not.toBeInTheDocument();
    expect(screen.getByText(/Showing/)).toHaveTextContent('Showing 6-7 of 7 shipments');
    expect(getShipments).toHaveBeenCalledWith(expect.objectContaining({ page: 2 }));
  });

  it('filters the shipment list by status', async () => {
    mockShipments(expandedShipments);
    renderQueue();

    fireEvent.click(await screen.findByRole('button', { name: /Delivered/ }));

    expect(await screen.findByText('LUNA-TRACK-s3')).toBeInTheDocument();
    expect(screen.queryByText('LUNA-TRACK-s1')).not.toBeInTheDocument();
    expect(getShipments).toHaveBeenCalledWith(expect.objectContaining({ status: 'Delivered' }));
  });

  it('sends the delivered command for shipments ready to close', async () => {
    mockShipments(expandedShipments);
    renderQueue();

    fireEvent.click((await screen.findAllByRole('button', { name: 'Mark Delivered' }))[0]);

    await waitFor(() => expect(markShipmentDelivered).toHaveBeenCalledWith('s2', expect.anything()));
  });

  it('shows the message returned by a failed shipment command', async () => {
    vi.mocked(markShipmentInTransit).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: 'The shipment was already in transit.' } },
    });

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Mark In Transit' }));

    expect(await screen.findByText('The shipment was already in transit.')).toBeInTheDocument();
  });

  it('falls back when the shipment command fails without a response message', async () => {
    vi.mocked(markShipmentInTransit).mockRejectedValue({ isAxiosError: true, response: { status: 500, data: {} } });

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Mark In Transit' }));

    expect(await screen.findByText('The shipment command could not be completed.')).toBeInTheDocument();
  });

  it('shows a load failure for the shipment queue', async () => {
    vi.mocked(getShipments).mockRejectedValue(new Error('Shipping unavailable.'));

    renderQueue();

    expect(await screen.findByText('Unable to load shipments. Refresh and try again.')).toBeInTheDocument();
  });

  it('shows an empty state when no shipments match the current filters', async () => {
    vi.mocked(getShipments).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5 });

    renderQueue();

    expect(await screen.findByText('No shipments match the current filters.')).toBeInTheDocument();
  });

  it('shows the pending label while the in-transit command is running', async () => {
    vi.mocked(markShipmentInTransit).mockReturnValue(new Promise(() => undefined));

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Mark In Transit' }));

    expect(await screen.findByText('Updating...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Updating...' })).toBeDisabled();
  });

  it('shows the pending label while the delivered command is running', async () => {
    mockShipments(expandedShipments);
    vi.mocked(markShipmentDelivered).mockReturnValue(new Promise(() => undefined));

    renderQueue();
    fireEvent.click((await screen.findAllByRole('button', { name: 'Mark Delivered' }))[0]);

    await waitFor(() => expect(markShipmentDelivered).toHaveBeenCalledWith('s2', expect.anything()));
    expect(await screen.findByText('Updating...')).toBeInTheDocument();
  });
});