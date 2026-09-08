import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  registerAccount: vi.fn(),
  push: vi.fn(),
}));

vi.mock('../../../lib/api/identity', () => ({ registerAccount: mocks.registerAccount }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push }) }));

import RegisterForm from '../../../components/auth/RegisterForm';

function renderRegisterForm() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <RegisterForm />
    </QueryClientProvider>,
  );
}

describe('RegisterForm', () => {
  beforeEach(() => mocks.registerAccount.mockReset());

  it('shows field-level errors for invalid registration data', async () => {
    renderRegisterForm();

    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    await waitFor(() => {
      expect(screen.getByText('First name is required.')).toBeInTheDocument();
      expect(screen.getByText('Please enter a valid email address.')).toBeInTheDocument();
      expect(screen.getByText('You must agree to the Terms of Service and Privacy Policy.')).toBeInTheDocument();
    });
    expect(mocks.registerAccount).not.toHaveBeenCalled();
  });

  it('submits valid credentials to Identity and shows success', async () => {
    mocks.registerAccount.mockResolvedValueOnce(undefined);
    renderRegisterForm();

    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Jane' } });
    fireEvent.change(screen.getByLabelText('Last name'), { target: { value: 'Doe' } });
    fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'jane@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    await waitFor(() => expect(mocks.registerAccount).toHaveBeenCalledWith({
      email: 'jane@example.com',
      password: 'Password123!',
      firstName: 'Jane',
      lastName: 'Doe',
    }, expect.anything()));
    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument();
  });

  it('shows registration errors and toggles password visibility', async () => {
    mocks.registerAccount.mockRejectedValueOnce({ isAxiosError: true, response: { status: 400 } });
    renderRegisterForm();

    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Jane' } });
    fireEvent.change(screen.getByLabelText('Last name'), { target: { value: 'Doe' } });
    fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'jane@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('This account could not be created. Check your details and try again.')).toBeInTheDocument();

    mocks.registerAccount.mockRejectedValueOnce(new Error('network failure'));
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));
    expect(await screen.findByText('Unable to create your account right now. Please try again.')).toBeInTheDocument();
  });
});
