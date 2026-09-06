import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { changeCartItemQuantity, getCart, removeCartItem } from '../../../lib/api/orders';
import { getProduct } from '../../../lib/api/catalog';
import CartView from '../../../components/cart/CartView';

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
});
