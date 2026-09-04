import { vi } from 'vitest';
import { identityApi, login } from '../../../lib/api/identity';

describe('Identity API client', () => {
  it('logs in with the cookie-based Identity endpoint', async () => {
    const post = vi.spyOn(identityApi, 'post').mockResolvedValueOnce({ data: null });

    await login({ email: 'jane@example.com', password: 'password123' });

    expect(post).toHaveBeenCalledWith('/login?useCookies=true', {
      email: 'jane@example.com',
      password: 'password123',
    });
  });
});
