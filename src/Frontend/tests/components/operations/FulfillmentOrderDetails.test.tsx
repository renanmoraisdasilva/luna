import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FulfillmentOrderDetails from '../../../components/operations/FulfillmentOrderDetails';
import { createShipment, prepareFulfillmentOrder, type FulfillmentOrder } from '../../../lib/api/orders';

const router = vi.hoisted(() => ({ refresh: vi.fn() }));

vi.mock('next/navigation', () => ({ useRouter: () => router }));
vi.mock('../../../lib/api/orders', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api/orders')>('../../../lib/api/orders');
  return { ...actual, createShipment: vi.fn(), prepareFulfillmentOrder: vi.fn() };
});

const confirmedOrder: FulfillmentOrder = {
  orderId: 'order-1',
  customerId: 'customer-1',
  customerName: 'Jane Doe',
  orderStatus: 'Confirmed',
  paymentStatus: 'Authorized',
  subtotal: 100,
  shippingCost: 5,
  total: 105,
  shippingMethodCode: 'STANDARD',
  createdAt: '2026-09-25T10:42:00Z',
  shippingAddress: { fullName: 'Jane Doe', addressLine1: '1 Main Street', addressLine2: null, city: 'Austin', stateOrProvince: 'TX', postalCode: '78701', country: 'US' },
  items: [],
  availableAction: 'StartPreparing',
};

function renderDetails(order: FulfillmentOrder = confirmedOrder) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><FulfillmentOrderDetails order={order} /></QueryClientProvider>);
}

describe('FulfillmentOrderDetails', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(prepareFulfillmentOrder).mockResolvedValue({ orderId: 'order-1', orderStatus: 'Preparing' });
    vi.mocked(createShipment).mockResolvedValue({ orderId: 'order-1', orderStatus: 'Shipped' });
  });

  it('prepares a confirmed order and refreshes the server-backed detail', async () => {
    renderDetails();

    fireEvent.click(screen.getByRole('button', { name: 'Start Order Preparation' }));

    await waitFor(() => expect(prepareFulfillmentOrder).toHaveBeenCalledWith('order-1'));
    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
  });

  it('shows a command failure while keeping the current order state visible', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValueOnce(new Error('Preparation failed.'));
    renderDetails();

    fireEvent.click(screen.getByRole('button', { name: 'Start Order Preparation' }));

    expect(await screen.findByText('The fulfillment command could not be completed.')).toBeInTheDocument();
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
    expect(router.refresh).not.toHaveBeenCalled();
  });

  it('creates a shipment when the order is ready for shipment', async () => {
    renderDetails({ ...confirmedOrder, orderStatus: 'Preparing', availableAction: 'CreateShipment' });

    fireEvent.click(screen.getByRole('button', { name: 'Create Shipment' }));

    await waitFor(() => expect(createShipment).toHaveBeenCalledWith('order-1'));
    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
  });

  it('renders a shipped order with no workflow command available', async () => {
    renderDetails({ ...confirmedOrder, orderStatus: 'Shipped', availableAction: 'None' });

    expect(screen.getByRole('heading', { name: 'No action available' })).toBeInTheDocument();
    expect(screen.getByText('Step 3 of 5')).toBeInTheDocument();
    expect(screen.getByText('The next workflow command will be available from this order state.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'No action available' })).toBeDisabled();
    expect(createShipment).not.toHaveBeenCalled();
  });

  it('renders a delivered order with no workflow command available', async () => {
    renderDetails({ ...confirmedOrder, orderStatus: 'Delivered', availableAction: 'None' });

    expect(screen.getByRole('heading', { name: 'No action available' })).toBeInTheDocument();
    expect(screen.getByText('Step 5 of 5')).toBeInTheDocument();
    expect(screen.getByText('The next workflow command will be available from this order state.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'No action available' })).toBeDisabled();
    expect(prepareFulfillmentOrder).not.toHaveBeenCalled();
    expect(createShipment).not.toHaveBeenCalled();
  });

  it('shows the pending label while the workflow command is running', async () => {
    vi.mocked(prepareFulfillmentOrder).mockReturnValue(new Promise(() => undefined));

    renderDetails();

    fireEvent.click(screen.getByRole('button', { name: 'Start Order Preparation' }));

    expect(await screen.findByRole('button', { name: 'Processing...' })).toBeDisabled();
  });

  it('shows the message returned by a failed workflow command', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: 'The order changed state before the command ran.' } },
    });

    renderDetails();
    fireEvent.click(screen.getByRole('button', { name: 'Start Order Preparation' }));

    expect(await screen.findByText('The order changed state before the command ran.')).toBeInTheDocument();
    expect(router.refresh).not.toHaveBeenCalled();
  });

  it('falls back when the workflow command fails without a response message', async () => {
    vi.mocked(prepareFulfillmentOrder).mockRejectedValue({ isAxiosError: true, response: { status: 500, data: {} } });

    renderDetails();
    fireEvent.click(screen.getByRole('button', { name: 'Start Order Preparation' }));

    expect(await screen.findByText('The fulfillment command could not be completed.')).toBeInTheDocument();
  });
});
