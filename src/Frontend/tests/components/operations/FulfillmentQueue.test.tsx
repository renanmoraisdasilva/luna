import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { createShipment, getFulfillmentQueue, prepareFulfillmentOrder, type FulfillmentOrderSummary } from '../../../lib/api/orders';
import FulfillmentQueue from '../../../components/operations/FulfillmentQueue';

vi.mock('../../../lib/api/orders', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api/orders')>('../../../lib/api/orders');
  return { ...actual, createShipment: vi.fn(), getFulfillmentQueue: vi.fn(), prepareFulfillmentOrder: vi.fn() };
});

const orders = [
  {
    orderId: 'order-1042',
    customerId: 'customer-1',
    customerName: 'John Smith',
    itemCount: 3,
    total: 155,
    paymentStatus: 'Authorized',
    orderStatus: 'Confirmed' as const,
    createdAt: '2026-09-20T10:42:00Z',
    availableAction: 'StartPreparing' as const,
  },
  {
    orderId: 'order-1039',
    customerId: 'customer-2',
    customerName: 'Joao Costa',
    itemCount: 5,
    total: 340,
    paymentStatus: 'Authorized',
    orderStatus: 'Preparing' as const,
    createdAt: '2026-09-19T16:40:00Z',
    availableAction: 'CreateShipment' as const,
  },
];

function mockQueue(items: FulfillmentOrderSummary[]) {
  vi.mocked(getFulfillmentQueue).mockImplementation(async (params) => {
    const filtered = items.filter((order) => {
      const matchesStatus = !params.status || order.orderStatus === params.status;
      const search = params.search?.toLowerCase() ?? '';
      const matchesSearch = !search || order.orderId.toLowerCase().includes(search) || order.customerName.toLowerCase().includes(search);
      return matchesStatus && matchesSearch;
    });

    const start = (params.page - 1) * params.pageSize;
    return { items: filtered.slice(start, start + params.pageSize), totalCount: filtered.length, page: params.page, pageSize: params.pageSize };
  });
}

const expandedOrders: FulfillmentOrderSummary[] = [
  { orderId: 'order-2001', customerId: 'customer-1', customerName: 'Alice Adams', itemCount: 1, total: 20, paymentStatus: 'Pending', orderStatus: 'Confirmed', createdAt: '2026-09-21T09:00:00Z', availableAction: 'StartPreparing' },
  { orderId: 'order-2002', customerId: 'customer-2', customerName: 'Ben Blake', itemCount: 2, total: 60, paymentStatus: 'Authorized', orderStatus: 'Preparing', createdAt: '2026-09-21T10:00:00Z', availableAction: 'CreateShipment' },
  { orderId: 'order-2003', customerId: 'customer-3', customerName: 'Cara Cruz', itemCount: 3, total: 30, paymentStatus: 'Authorized', orderStatus: 'Preparing', createdAt: '2026-09-21T11:00:00Z', availableAction: 'CreateShipment' },
  { orderId: 'order-2004', customerId: 'customer-4', customerName: 'Diego Diaz', itemCount: 2, total: 45, paymentStatus: 'Authorized', orderStatus: 'Shipped', createdAt: '2026-09-21T12:00:00Z', availableAction: 'None' },
  { orderId: 'order-2005', customerId: 'customer-5', customerName: 'Emma Ellis', itemCount: 1, total: 15, paymentStatus: 'Refunded', orderStatus: 'Delivered', createdAt: '2026-09-21T13:00:00Z', availableAction: 'None' },
  { orderId: 'order-2006', customerId: 'customer-6', customerName: 'Felix Foster', itemCount: 4, total: 80, paymentStatus: 'Authorized', orderStatus: 'Confirmed', createdAt: '2026-09-21T14:00:00Z', availableAction: 'StartPreparing' },
  { orderId: 'order-2007', customerId: 'customer-7', customerName: 'Gina Gonzalez', itemCount: 1, total: 25, paymentStatus: 'Authorized', orderStatus: 'Preparing', createdAt: '2026-09-21T15:00:00Z', availableAction: 'CreateShipment' },
];

function renderQueue() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><FulfillmentQueue /></QueryClientProvider>);
}

describe('FulfillmentQueue', () => {
  beforeEach(() => {
    vi.mocked(prepareFulfillmentOrder).mockResolvedValue({ orderId: 'order-1042', orderStatus: 'Preparing' });
    vi.mocked(createShipment).mockResolvedValue({ orderId: 'order-1039', orderStatus: 'Shipped' });
    vi.mocked(getFulfillmentQueue).mockImplementation(async (params) => {
      const filtered = orders.filter((order) => {
        const matchesStatus = !params.status || order.orderStatus === params.status;
        const search = params.search?.toLowerCase() ?? '';
        const matchesSearch = !search || order.orderId.toLowerCase().includes(search) || order.customerName.toLowerCase().includes(search);
        return matchesStatus && matchesSearch;
      });

      const start = (params.page - 1) * params.pageSize;
      return { items: filtered.slice(start, start + params.pageSize), totalCount: filtered.length, page: params.page, pageSize: params.pageSize };
    });
  });

  afterEach(() => vi.clearAllMocks());

  it('loads and filters orders through the fulfillment endpoint', async () => {
    renderQueue();

    expect(await screen.findByText('#order-1042')).toBeInTheDocument();
    expect(screen.getByText('#order-1039')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^Preparing/ }));

    expect(await screen.findByText('#order-1039')).toBeInTheDocument();
    expect(screen.queryByText('#order-1042')).not.toBeInTheDocument();

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search fulfillment orders' }), { target: { value: 'Joao' } });

    expect(await screen.findByText('#order-1039')).toBeInTheDocument();
    expect(getFulfillmentQueue).toHaveBeenCalledWith(expect.objectContaining({ status: 'Preparing', search: 'Joao' }));
  });

  it('executes preparation and create shipment commands', async () => {
    renderQueue();

    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));

    await waitFor(() => expect(prepareFulfillmentOrder).toHaveBeenCalledWith('order-1042', expect.anything()));

    fireEvent.click(screen.getByRole('button', { name: 'Create Shipment' }));
    await waitFor(() => expect(createShipment).toHaveBeenCalledWith('order-1039', expect.anything()));
  });

  it('shows a preparation command failure without changing the queue state', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValueOnce(new Error('Preparation failed.'));

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));

    expect(await screen.findByText('The fulfillment command could not be completed.')).toBeInTheDocument();
    expect(screen.getByText('#order-1042')).toBeInTheDocument();
  });

  it('renders mixed order states and paginates the queue', async () => {
    mockQueue(expandedOrders);
    renderQueue();

    expect(await screen.findByText('#order-2001')).toBeInTheDocument();
    expect(screen.getByText('#order-2002')).toBeInTheDocument();
    expect(screen.getAllByText('1 item').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Unavailable')).toHaveLength(2);
    expect(screen.getAllByText('Create Shipment').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Next page' })).not.toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('#order-2007')).toBeInTheDocument();
    expect(screen.queryByText('#order-2001')).not.toBeInTheDocument();
    expect(screen.getByText(/Showing/)).toHaveTextContent('Showing 6-7 of 7 orders');
    expect(getFulfillmentQueue).toHaveBeenCalledWith(expect.objectContaining({ page: 2 }));
  });

  it('shows an empty state when no orders match the current filters', async () => {
    vi.mocked(getFulfillmentQueue).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5 });

    renderQueue();

    expect(await screen.findByText('No fulfillment orders match the current filters.')).toBeInTheDocument();
  });

  it('shows a load failure for the fulfillment queue', async () => {
    vi.mocked(getFulfillmentQueue).mockRejectedValue(new Error('Orders unavailable.'));

    renderQueue();

    expect(await screen.findByText('Unable to load fulfillment orders. Refresh and try again.')).toBeInTheDocument();
  });

  it('shows the message returned by a failed fulfillment command', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: 'Inventory is still reserved.' } },
    });

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));

    expect(await screen.findByText('Inventory is still reserved.')).toBeInTheDocument();
  });

  it('falls back when the shipment command fails without a response message', async () => {
    vi.mocked(createShipment).mockRejectedValue({ isAxiosError: true, response: { status: 500, data: {} } });

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Create Shipment' }));

    expect(await screen.findByText('The fulfillment command could not be completed.')).toBeInTheDocument();
  });

  it('shows the pending label while each fulfillment command is running', async () => {
    vi.mocked(prepareFulfillmentOrder).mockReturnValue(new Promise(() => undefined));
    vi.mocked(createShipment).mockReturnValue(new Promise(() => undefined));

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));
    expect(await screen.findByText('Starting...')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Create Shipment' }));
    expect(await screen.findByText('Creating...')).toBeInTheDocument();
  });
});
