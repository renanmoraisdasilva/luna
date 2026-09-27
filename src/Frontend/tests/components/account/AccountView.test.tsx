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

  it('surfaces profile validation errors next to the offending fields', async () => {
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    const firstName = screen.getByLabelText('First name');
    const lastName = screen.getByLabelText('Last name');

    fireEvent.change(firstName, { target: { value: '' } });
    fireEvent.change(lastName, { target: { value: ' ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('First name is required.')).toBeInTheDocument();
    expect(screen.getByText('Last name is required.')).toBeInTheDocument();
    expect(firstName).toHaveAttribute('aria-invalid', 'true');
    expect(firstName).toHaveAttribute('aria-describedby', 'firstName-error');
    expect(lastName).toHaveAttribute('aria-invalid', 'true');
    expect(lastName).toHaveAttribute('aria-describedby', 'lastName-error');
    expect(updateProfile).not.toHaveBeenCalled();
  });

  it('surfaces password validation errors next to the offending fields', async () => {
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    const currentPassword = screen.getByLabelText('Current password');
    const newPassword = screen.getByLabelText('New password');

    fireEvent.change(newPassword, { target: { value: 'short' } });
    fireEvent.click(screen.getByRole('button', { name: 'Update password' }));

    expect(await screen.findByText('Current password is required.')).toBeInTheDocument();
    expect(screen.getByText('New password must be at least 6 characters.')).toBeInTheDocument();
    expect(currentPassword).toHaveAttribute('aria-invalid', 'true');
    expect(currentPassword).toHaveAttribute('aria-describedby', 'currentPassword-error');
    expect(newPassword).toHaveAttribute('aria-invalid', 'true');
    expect(newPassword).toHaveAttribute('aria-describedby', 'newPassword-error');
    expect(changePassword).not.toHaveBeenCalled();
  });

  it('shows the signing-out state while logout is running', async () => {
    vi.mocked(logout).mockReturnValue(new Promise<void>(() => undefined));
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };

    render(<QueryClientProvider client={new QueryClient()}><AccountView initialUser={user} /></QueryClientProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Logout' }));

    expect(await screen.findByRole('button', { name: 'Signing out...' })).toBeDisabled();
    expect(mocks.replace).not.toHaveBeenCalled();
  });
});
