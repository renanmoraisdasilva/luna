import { vi } from 'vitest';
import { identityApi, login, registerAccount } from '../../../lib/api/identity';

describe('Identity API client', () => {
  it('logs in through the same-origin gateway route', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ authenticated: true }), { status: 200 }),
    );

    await login({ email: 'jane@example.com', password: 'password123' });

    expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email: 'jane@example.com', password: 'password123' }),
    });
  });

  it('registers an account with the Identity endpoint', async () => {
    const post = vi.spyOn(identityApi, 'post').mockResolvedValueOnce({ data: null });

    await registerAccount({
      email: 'jane@example.com',
      password: 'password123',
      firstName: 'Jane',
      lastName: 'Doe',
    });

    expect(post).toHaveBeenCalledWith('/register-profile', {
      email: 'jane@example.com',
      password: 'password123',
      firstName: 'Jane',
      lastName: 'Doe',
    });
  });

  it('surfaces a failed login response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ message: 'Invalid credentials.' }), { status: 401 }),
    );

    await expect(login({ email: 'jane@example.com', password: 'wrong-password' }))
      .rejects.toThrow('Unable to sign in.');
  });

  it('surfaces a failed registration response', async () => {
    vi.spyOn(identityApi, 'post').mockRejectedValueOnce(new Error('Identity unavailable.'));

    await expect(registerAccount({
      email: 'jane@example.com',
      password: 'password123',
      firstName: 'Jane',
      lastName: 'Doe',
    })).rejects.toThrow('Identity unavailable.');
  });
});
