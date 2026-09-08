import { vi } from 'vitest';
import { changePassword, getCurrentUser, logout, updateProfile } from '../../../lib/api/auth';

describe('Auth API client', () => {
  it('returns null for an unauthenticated user', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(null, { status: 401 }));

    await expect(getCurrentUser()).resolves.toBeNull();
  });

  it('returns the current user', async () => {
    const user = { id: 'customer-1', email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe' };
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(JSON.stringify(user), { status: 200 }));

    await expect(getCurrentUser()).resolves.toEqual(user);
  });

  it('surfaces unexpected current-user and profile failures', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(new Response(null, { status: 400 }));

    await expect(getCurrentUser()).rejects.toThrow('Authentication request failed.');
    await expect(updateProfile({ firstName: 'Jane', lastName: 'Doe' })).rejects.toThrow('Profile update failed.');
  });

  it('sends profile updates and password changes', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'customer-1' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await updateProfile({ firstName: 'Janet', lastName: 'Doe' });
    await changePassword('old-password', 'new-password');

    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/auth/profile', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ firstName: 'Janet', lastName: 'Doe' }),
    });
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/auth/password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword: 'old-password', newPassword: 'new-password' }),
    });
  });

  it('surfaces failed logout and password changes', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 500 }))
      .mockResolvedValueOnce(new Response(null, { status: 400 }));

    await expect(logout()).rejects.toThrow('Logout failed.');
    await expect(changePassword('old-password', 'new-password')).rejects.toThrow('Password change failed.');
  });
});
