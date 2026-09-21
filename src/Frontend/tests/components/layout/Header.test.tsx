import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Header from '../../../components/layout/Header';

const mocks = vi.hoisted(() => ({
  getCart: vi.fn(),
  useCurrentUser: vi.fn(),
}));

vi.mock('../../../lib/api/orders', () => ({ getCart: mocks.getCart }));
vi.mock('../../../lib/queries/auth', () => ({ useCurrentUser: mocks.useCurrentUser }));

function renderHeader() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={queryClient}>
        <Header />
      </QueryClientProvider>,
    );
}

describe('Header', () => {
  beforeEach(() => {
    mocks.getCart.mockResolvedValue({ items: [] });
  });

  it('renders operations only for admins and restores utility links', () => {
    mocks.useCurrentUser.mockReturnValue({ data: { roles: ['Admin'] } });
    renderHeader();

    expect(screen.getByRole('link', { name: 'Luna' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Shop' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Orders' })).toHaveAttribute('href', '/orders');
    expect(screen.getByRole('link', { name: 'Operations' })).toHaveAttribute('href', '/operations/fulfillment');
    expect(screen.getByRole('link', { name: 'Swagger' })).toHaveAttribute('href', '/swagger');
    expect(screen.getByRole('link', { name: 'SigNoz' })).toHaveAttribute('href', 'http://localhost:8080');
    expect(screen.getByRole('link', { name: 'SigNoz' })).toHaveAttribute('target', '_blank');
    expect(screen.getByRole('link', { name: 'Account' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'Cart' })).toHaveAttribute('href', '/cart');
  });

  it('hides operations from non-admin users', () => {
    mocks.useCurrentUser.mockReturnValue({ data: { roles: ['Customer'] } });
    renderHeader();

    expect(screen.queryByRole('link', { name: 'Operations' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Swagger' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'SigNoz' })).toBeInTheDocument();
  });
});
