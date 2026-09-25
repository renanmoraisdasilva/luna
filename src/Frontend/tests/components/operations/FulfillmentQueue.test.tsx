import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { createShipment, getFulfillmentQueue, prepareFulfillmentOrder } from '../../../lib/api/orders';
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
    orderStatus: 'ShippingPendingRetry' as const,
    createdAt: '2026-09-19T16:40:00Z',
    availableAction: 'RetryShipment' as const,
  },
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

    fireEvent.click(screen.getByRole('button', { name: /Shipping Pending Retry/ }));

    expect(await screen.findByText('#order-1039')).toBeInTheDocument();
    expect(screen.queryByText('#order-1042')).not.toBeInTheDocument();

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search fulfillment orders' }), { target: { value: 'Joao' } });

    expect(await screen.findByText('#order-1039')).toBeInTheDocument();
    expect(getFulfillmentQueue).toHaveBeenCalledWith(expect.objectContaining({ status: 'ShippingPendingRetry', search: 'Joao' }));
  });

  it('executes preparation and retry shipment commands', async () => {
    renderQueue();

    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));

    await waitFor(() => expect(prepareFulfillmentOrder).toHaveBeenCalledWith('order-1042', expect.anything()));

    fireEvent.click(screen.getByRole('button', { name: 'Retry Shipment' }));
    await waitFor(() => expect(createShipment).toHaveBeenCalledWith('order-1039'));
  });

  it('shows a preparation command failure without changing the queue state', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValueOnce(new Error('Preparation failed.'));

    renderQueue();
    fireEvent.click(await screen.findByRole('button', { name: 'Start Preparing' }));

    expect(await screen.findByText('The fulfillment command could not be completed.')).toBeInTheDocument();
    expect(screen.getByText('#order-1042')).toBeInTheDocument();
  });
});
