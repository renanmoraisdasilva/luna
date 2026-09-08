import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { changeCartItemQuantity, getCart, removeCartItem } from '../../../lib/api/orders';
import { getProduct } from '../../../lib/api/catalog';
import CartView from '../../../components/cart/CartView';

const router = vi.hoisted(() => ({ replace: vi.fn() }));

vi.mock('next/navigation', () => ({
  usePathname: () => '/cart',
  useRouter: () => router,
  useSearchParams: () => new URLSearchParams('from=header'),
}));

vi.mock('../../../lib/api/orders', () => ({
  changeCartItemQuantity: vi.fn(),
  getCart: vi.fn(),
  removeCartItem: vi.fn(),
}));

vi.mock('../../../lib/api/catalog', () => ({ getProduct: vi.fn() }));

function renderCart() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <CartView />
    </QueryClientProvider>,
  );
}

describe('CartView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getCart).mockResolvedValue({ id: 'cart-1', customerId: 'customer-1', items: [{ productId: 'product-1', quantity: 2 }] });
    vi.mocked(getProduct).mockResolvedValue({
      id: 'product-1', name: 'Keyboard', currentPrice: 20, images: [],
    } as never);
  });

  it('changes item quantity and refreshes the cart', async () => {
    vi.mocked(changeCartItemQuantity).mockResolvedValue({ id: 'cart-1', customerId: 'customer-1', items: [{ productId: 'product-1', quantity: 3 }] });

    renderCart();
    await waitFor(() => expect(screen.getByText('Keyboard')).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'Increase quantity for Keyboard' }));

    await waitFor(() => expect(changeCartItemQuantity).toHaveBeenCalledWith('product-1', 3));
    await waitFor(() => expect(getCart).toHaveBeenCalledTimes(2));
  });

  it('removes an item and refreshes the cart', async () => {
    vi.mocked(removeCartItem).mockResolvedValue();

    renderCart();
    await waitFor(() => expect(screen.getByText('Keyboard')).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'Remove Keyboard' }));

    await waitFor(() => expect(removeCartItem).toHaveBeenCalledWith('product-1', expect.any(Object)));
    await waitFor(() => expect(getCart).toHaveBeenCalledTimes(2));
  });

  it('shows an error when changing quantity fails', async () => {
    vi.mocked(changeCartItemQuantity).mockRejectedValueOnce(new Error('Cart update failed.'));

    renderCart();
    await waitFor(() => expect(screen.getByText('Keyboard')).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'Increase quantity for Keyboard' }));

    await waitFor(() => expect(screen.getByText('Cart update failed.')).toBeInTheDocument());
  });

  it('redirects to login when loading the cart is unauthorized', async () => {
    vi.mocked(getCart).mockRejectedValueOnce({ response: { status: 401 } });

    renderCart();

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/login?returnUrl=%2Fcart%3Ffrom%3Dheader'));
    expect(screen.getByText('We could not load your cart.')).toBeInTheDocument();
  });

  it('shows the empty cart state', async () => {
    vi.mocked(getCart).mockResolvedValueOnce({ id: 'cart-1', customerId: 'customer-1', items: [] });

    renderCart();

    await waitFor(() => expect(screen.getByText('Your cart is empty')).toBeInTheDocument());
    expect(getProduct).not.toHaveBeenCalled();
  });

  it('shows unavailable products and uses a fallback mutation error', async () => {
    vi.mocked(getProduct).mockResolvedValueOnce(null);
    vi.mocked(removeCartItem).mockRejectedValueOnce({ response: { status: 500 } });

    renderCart();
    await waitFor(() => expect(screen.getByText('Product unavailable')).toBeInTheDocument());
    expect(screen.getByText('One or more products are no longer available. Remove them before checkout.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Remove unavailable product' }));
    await waitFor(() => expect(screen.getByText('We could not update your cart.')).toBeInTheDocument());
  });

  it('disables decreasing a single-item quantity', async () => {
    vi.mocked(getCart).mockResolvedValueOnce({ id: 'cart-1', customerId: 'customer-1', items: [{ productId: 'product-1', quantity: 1 }] });

    renderCart();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Decrease quantity for Keyboard' })).toBeDisabled());
  });
});
