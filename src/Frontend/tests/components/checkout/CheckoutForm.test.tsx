import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CheckoutForm from '../../../components/checkout/CheckoutForm';
import { submitCheckout } from '../../../lib/api/orders';

const router = vi.hoisted(() => ({ replace: vi.fn() }));

vi.mock('../../../lib/api/orders', () => ({
  submitCheckout: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => router,
}));

const cart = {
  id: 'cart-1',
  customerId: 'customer-1',
  items: [{ productId: 'product-1', quantity: 1 }],
};

const products = {
  'product-1': {
    id: 'product-1',
    name: 'Luna Keyboard',
    currentPrice: 12.5,
    images: [],
  },
};

const shippingMethods = [{ id: 'shipping-1', code: 'STANDARD', name: 'Standard', cost: 4.99, estimatedDeliveryDays: 5 }];

function renderForm() {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}>
      <CheckoutForm cart={cart} products={products} shippingMethods={shippingMethods} email="jane@example.com" />
    </QueryClientProvider>,
  );
}

async function fillRequiredFields() {
  const fields = [
    ['Full name', 'Jane Doe'],
    ['Payment method', 'test-card'],
    ['Address', '123 Luna Street'],
    ['City', 'Austin'],
    ['State / province', 'Texas'],
    ['Postal code', '78701'],
  ] as const;

  for (const [label, value] of fields) {
    const input = screen.getByLabelText(label);
    fireEvent.change(input, { target: { name: input.getAttribute('name'), value } });
    fireEvent.blur(input);
    await waitFor(() => expect(input).toHaveValue(value));
  }
}

describe('CheckoutForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('submits a valid checkout and navigates to confirmation', async () => {
    vi.mocked(submitCheckout).mockResolvedValue({
      orderId: 'order-1',
      status: 'Confirmed',
      total: 17.49,
      currency: 'USD',
      reservationId: 'reservation-1',
      paymentId: 'payment-1',
    });

    renderForm();
    await fillRequiredFields();
    fireEvent.click(screen.getByRole('button', { name: 'Place order' }));

    await waitFor(() => expect(submitCheckout).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/checkout/confirmation?orderId=order-1'));
  });

  it('disables duplicate submission while checkout is pending', async () => {
    let resolveCheckout: (value: { orderId: string; status: string; total: number; currency: string; reservationId: string; paymentId: string }) => void = () => undefined;
    vi.mocked(submitCheckout).mockReturnValue(new Promise((resolve) => { resolveCheckout = resolve; }));

    renderForm();
    await fillRequiredFields();
    const button = screen.getByRole('button', { name: 'Place order' });
    fireEvent.click(button);

    await waitFor(() => expect(submitCheckout).toHaveBeenCalledTimes(1));
    expect(button).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Placing order...' })).toBeDisabled();

    fireEvent.click(button);
    expect(submitCheckout).toHaveBeenCalledTimes(1);

    resolveCheckout({
      orderId: 'order-1',
      status: 'Confirmed',
      total: 17.49,
      currency: 'USD',
      reservationId: 'reservation-1',
      paymentId: 'payment-1',
    });
  });

  it('shows a customer-friendly downstream checkout error', async () => {
    vi.mocked(submitCheckout).mockRejectedValue({
      isAxiosError: true,
      response: { status: 422, data: { message: 'Payment authorization was declined.' } },
    });

    renderForm();
    await fillRequiredFields();
    fireEvent.click(screen.getByRole('button', { name: 'Place order' }));

    expect(await screen.findByText('Payment authorization was declined.')).toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it('shows a safe fallback when checkout is unauthorized', async () => {
    vi.mocked(submitCheckout).mockRejectedValue({ isAxiosError: true, response: { status: 401 } });

    renderForm();
    await fillRequiredFields();
    fireEvent.click(screen.getByRole('button', { name: 'Place order' }));

    expect(await screen.findByText('We could not place your order.')).toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
  });
});
