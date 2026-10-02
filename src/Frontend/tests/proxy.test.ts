import { NextRequest } from 'next/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const decryptAccessToken = vi.hoisted(() => vi.fn());
vi.mock('../lib/auth-cookie', () => ({
  accessTokenCookieName: () => 'luna_access_token',
  isSecureCookieRequired: () => true,
  decryptAccessToken,
}));

import { proxy } from '../proxy';

function request(path: string, token?: string) {
  return new NextRequest(`http://localhost${path}`, token ? { headers: { cookie: `luna_access_token=${token}` } } : undefined);
}

describe('frontend proxy', () => {
  beforeEach(() => {
    decryptAccessToken.mockReset();
  });

  it('allows public pages without a token', async () => {
    const result = await proxy(request('/shop'));

    expect(result.headers.get('location')).toBeNull();
  });

  it('redirects unauthenticated account pages to login with a return URL', async () => {
    const result = await proxy(request('/account/orders?filter=open'));

    expect(result.headers.get('location')).toBe('http://localhost/login?returnUrl=%2Faccount%2Forders%3Ffilter%3Dopen');
  });

  it('redirects unauthenticated checkout pages to login with a return URL', async () => {
    const result = await proxy(request('/checkout'));

    expect(result.headers.get('location')).toBe('http://localhost/login?returnUrl=%2Fcheckout');
  });

  it('allows authenticated pages and injects the token for service requests', async () => {
    decryptAccessToken.mockResolvedValue('access-token');

    const pageResult = await proxy(request('/account', 'encrypted'));
    const serviceResult = await proxy(request('/api/services/catalog/products', 'encrypted'));

    expect(pageResult.headers.get('location')).toBeNull();
    expect(serviceResult.headers.get('x-middleware-next')).toBe('1');
    expect(serviceResult.headers.get('x-middleware-request-authorization')).toBe('Bearer access-token');
  });

  it('continues when an encrypted token cannot be decrypted', async () => {
    decryptAccessToken.mockRejectedValue(new Error('invalid token'));

    const result = await proxy(request('/api/services/catalog/products', 'encrypted'));

    expect(result.headers.get('x-middleware-next')).toBe('1');
  });
});
