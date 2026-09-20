import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import Header from '../../../components/layout/Header';

describe('Header', () => {
  it('renders the storefront navigation links', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <Header />
      </QueryClientProvider>,
    );

    expect(screen.getByRole('link', { name: 'Luna' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Shop' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Orders' })).toHaveAttribute('href', '/orders');
    expect(screen.getByRole('link', { name: 'Operations' })).toHaveAttribute('href', '/operations/fulfillment');
    expect(screen.queryByRole('link', { name: 'Swagger' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'SigNoz' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Account' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'Cart' })).toHaveAttribute('href', '/cart');
  });
});
