import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  login: vi.fn(),
  push: vi.fn(),
  removeQueries: vi.fn(),
}));

vi.mock('../../../lib/api/identity', () => ({ login: mocks.login }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push }) }));

import LoginForm from '../../../components/auth/LoginForm';

function renderLoginForm() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  queryClient.removeQueries = mocks.removeQueries;
  return render(
    <QueryClientProvider client={queryClient}>
      <LoginForm />
    </QueryClientProvider>,
  );
}

describe('LoginForm', () => {
  beforeEach(() => {
    mocks.login.mockReset();
    mocks.push.mockReset();
    mocks.removeQueries.mockReset();
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
    await waitFor(() => expect(mocks.removeQueries).toHaveBeenCalledWith({ queryKey: ['current-user'] }));
    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith('/'));
  });
});
