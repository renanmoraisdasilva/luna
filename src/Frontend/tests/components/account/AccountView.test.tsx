import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountView from '../../../components/account/AccountView';
import { logout } from '../../../lib/api/auth';

const mocks = vi.hoisted(() => ({ replace: vi.fn() }));

vi.mock('../../../lib/api/auth', () => ({
  changePassword: vi.fn(),
  getCurrentUser: vi.fn(),
  logout: vi.fn(),
  updateProfile: vi.fn(),
}));

vi.mock('next/navigation', () => ({ useRouter: () => mocks }));

describe('AccountView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(logout).mockResolvedValue();
  });

  it('clears user and cart caches when logging out', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };
    queryClient.setQueryData(['cart'], { id: 'cart-1', customerId: 'customer-1', items: [] });

    render(
      <QueryClientProvider client={queryClient}>
        <AccountView initialUser={user} />
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Logout' }));

    await waitFor(() => expect(logout).toHaveBeenCalledOnce());
    expect(queryClient.getQueryData(['current-user'])).toBeNull();
    expect(queryClient.getQueryData(['cart'])).toBeUndefined();
    expect(mocks.replace).toHaveBeenCalledWith('/login');
  });
});
