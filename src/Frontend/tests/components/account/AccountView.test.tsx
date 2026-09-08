import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountView from '../../../components/account/AccountView';
import { changePassword, logout, updateProfile } from '../../../lib/api/auth';

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

  it('saves profile changes and displays the success state', async () => {
    vi.mocked(updateProfile).mockResolvedValue({ id: 'customer-1', email: 'jane@example.com', firstName: 'Janet', lastName: 'Doe' });
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Janet' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(updateProfile).toHaveBeenCalledWith({ firstName: 'Janet', lastName: 'Doe' }));
    expect(await screen.findByText('Changes saved')).toBeInTheDocument();
  });

  it('shows profile and password mutation failures', async () => {
    vi.mocked(updateProfile).mockRejectedValueOnce(new Error('profile failed'));
    vi.mocked(changePassword).mockRejectedValueOnce(new Error('password failed'));
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(screen.getByText('Unable to save changes.')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    fireEvent.change(screen.getByLabelText('Current password'), { target: { value: 'old-password' } });
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'new-password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Update password' }));
    await waitFor(() => expect(screen.getByText('Unable to update password.')).toBeInTheDocument());
  });

  it('updates the password and closes the form on success', async () => {
    vi.mocked(changePassword).mockResolvedValueOnce();
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    fireEvent.change(screen.getByLabelText('Current password'), { target: { value: 'old-password' } });
    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'new-password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Update password' }));

    await waitFor(() => expect(changePassword).toHaveBeenCalledWith('old-password', 'new-password'));
    expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument();
  });
});
