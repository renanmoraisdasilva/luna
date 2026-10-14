import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { addCartItem } from '../../../lib/api/orders';
import AddToCartButton from '../../../components/cart/AddToCartButton';

const mocks = {
  push: vi.fn(),
};

function renderButton(props: { productId: string; quantity?: number }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AddToCartButton {...props} />
    </QueryClientProvider>,
  );
}

vi.mock('../../../lib/api/orders', () => ({
  addCartItem: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/shop/products/product-1',
  useRouter: () => ({ push: mocks.push }),
  useSearchParams: () => new URLSearchParams('search=keyboard'),
}));

describe('AddToCartButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('adds the selected product and quantity to the cart', async () => {
    vi.mocked(addCartItem).mockResolvedValue({
      id: 'cart-1',
      customerId: 'customer-1',
      items: [{ productId: 'product-1', quantity: 2 }],
    });

    renderButton({ productId: 'product-1', quantity: 2 });
    fireEvent.click(screen.getByRole('button', { name: 'Add to Cart' }));

    await waitFor(() => expect(addCartItem).toHaveBeenCalledWith({ productId: 'product-1', quantity: 2 }));
    expect(screen.getByRole('button', { name: 'Added to Cart' })).toBeInTheDocument();
  });

  it('redirects unauthenticated customers to login with the current URL', async () => {
    vi.mocked(addCartItem).mockRejectedValue({ response: { status: 401 } });

    renderButton({ productId: 'product-1' });
    fireEvent.click(screen.getByRole('button', { name: 'Add to Cart' }));

    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith('/login?returnUrl=%2Fshop%2Fproducts%2Fproduct-1%3Fsearch%3Dkeyboard'));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows the backend message when adding the item fails', async () => {
    vi.mocked(addCartItem).mockRejectedValue({
      response: { status: 500, data: { code: 'INTERNAL_ERROR', message: 'An unexpected error occurred.' } },
    });

    renderButton({ productId: 'product-1' });
    fireEvent.click(screen.getByRole('button', { name: 'Add to Cart' }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('An unexpected error occurred.'));
    expect(mocks.push).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Add to Cart' })).toBeInTheDocument();
  });

  it('falls back to a friendly message when the failure carries no structured body', async () => {
    vi.mocked(addCartItem).mockRejectedValue(new Error('Request failed with status code 500'));

    renderButton({ productId: 'product-1' });
    fireEvent.click(screen.getByRole('button', { name: 'Add to Cart' }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('We could not add this item to your cart.'));
    expect(screen.queryByText('Request failed with status code 500')).not.toBeInTheDocument();
  });
});
