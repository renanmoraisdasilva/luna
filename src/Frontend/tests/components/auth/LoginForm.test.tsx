import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  login: vi.fn(),
}));

vi.mock('../../../lib/api/identity', () => ({ login: mocks.login }));

import LoginForm from '../../../components/auth/LoginForm';

function renderLoginForm() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <LoginForm />
    </QueryClientProvider>,
  );
}

describe('LoginForm', () => {
  beforeEach(() => {
    mocks.login.mockReset();
  });

  it('validates credentials before calling Identity', async () => {
    renderLoginForm();

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(screen.getByText('Please enter a valid email address.')).toBeInTheDocument());
    expect(mocks.login).not.toHaveBeenCalled();
  });

  it('submits credentials and redirects to the home page', async () => {
    mocks.login.mockResolvedValueOnce(undefined);
    renderLoginForm();

    fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'jane@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(mocks.login).toHaveBeenCalledWith(
      { email: 'jane@example.com', password: 'password123' },
      expect.anything(),
    ));
  });

  it('shows specific and generic login errors and toggles password visibility', async () => {
    mocks.login.mockRejectedValueOnce({ isAxiosError: true, response: { status: 401 } });
    renderLoginForm();

    fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'jane@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');
    fireEvent.click(screen.getByRole('button', { name: 'Hide password' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument();

    mocks.login.mockRejectedValueOnce(new Error('network failure'));
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByText('Unable to sign in right now. Please try again.')).toBeInTheDocument();
  });
});
