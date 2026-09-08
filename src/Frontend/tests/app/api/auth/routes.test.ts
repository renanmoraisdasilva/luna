import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  encryptAccessToken: vi.fn(),
  getAccessToken: vi.fn(),
  getIdentityUrl: vi.fn(),
}));

vi.mock('../../../../lib/auth-cookie', () => ({
  accessTokenCookieName: 'luna_access_token',
  encryptAccessToken: mocks.encryptAccessToken,
}));

vi.mock('../../../../lib/auth-server', () => ({
  getAccessToken: mocks.getAccessToken,
  getIdentityUrl: mocks.getIdentityUrl,
}));

import { POST as login } from '../../../../app/api/auth/login/route';
import { POST as logout } from '../../../../app/api/auth/logout/route';
import { POST as password } from '../../../../app/api/auth/password/route';
import { GET as me } from '../../../../app/api/auth/me/route';
import { PUT as profile } from '../../../../app/api/auth/profile/route';

function response(status: number, body = '{}', contentType = 'application/json') {
  return new Response(status === 204 ? null : body, { status, headers: { 'Content-Type': contentType } });
}

async function jsonResponse(result: Response) {
  return result.json() as Promise<Record<string, unknown>>;
}

describe('auth API routes', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    mocks.encryptAccessToken.mockReset();
    mocks.getAccessToken.mockReset();
    mocks.getIdentityUrl.mockReset();
    vi.stubEnv('IDENTITY_API_INTERNAL_URL', 'http://identity');
    mocks.getIdentityUrl.mockReturnValue('http://identity');
    mocks.getAccessToken.mockResolvedValue('access-token');
    vi.stubGlobal('fetch', vi.fn());
  });

  it('rejects login when Identity is not configured', async () => {
    vi.stubEnv('IDENTITY_API_INTERNAL_URL', '');

    const result = await login(new Request('http://localhost/api/auth/login', { method: 'POST', body: '{}' }));

    expect(result.status).toBe(500);
    expect(await jsonResponse(result)).toEqual({ message: 'Identity API is not configured.' });
  });

  it('forwards failed Identity login responses', async () => {
    vi.mocked(fetch).mockResolvedValue(response(401, 'invalid', 'text/plain'));

    const result = await login(new Request('http://localhost/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email: 'jane@example.com', password: 'bad' }),
    }));

    expect(result.status).toBe(401);
    expect(await result.text()).toBe('invalid');
    expect(result.headers.get('Content-Type')).toBe('text/plain');
  });

  it('rejects a successful Identity response without a token', async () => {
    vi.mocked(fetch).mockResolvedValue(response(200, JSON.stringify({})));

    const result = await login(new Request('http://localhost/api/auth/login', { method: 'POST', body: '{}' }));

    expect(result.status).toBe(502);
    expect(await jsonResponse(result)).toEqual({ message: 'Identity did not return an access token.' });
  });

  it('encrypts the token and sets the session cookie', async () => {
    mocks.encryptAccessToken.mockResolvedValue('encrypted-token');
    vi.stubEnv('AUTH_COOKIE_SECURE', 'true');
    vi.mocked(fetch).mockResolvedValue(response(200, JSON.stringify({ access_token: 'raw-token', expires_in: 90 })));

    const result = await login(new Request('http://localhost/api/auth/login', { method: 'POST', body: '{}' }));

    expect(result.status).toBe(200);
    expect(await jsonResponse(result)).toEqual({ authenticated: true });
    expect(mocks.encryptAccessToken).toHaveBeenCalledWith('raw-token');
    expect(result.headers.get('set-cookie')).toContain('luna_access_token=encrypted-token');
    expect(result.headers.get('set-cookie')).toContain('Max-Age=90');
    expect(result.headers.get('set-cookie')).toContain('Secure');
  });

  it('logs out by deleting the access token cookie', async () => {
    const result = await logout();

    expect(result.status).toBe(200);
    expect(await jsonResponse(result)).toEqual({ authenticated: false });
    expect(result.headers.get('set-cookie')).toContain('luna_access_token=;');
  });

  it('returns unauthorized when no access token is available', async () => {
    mocks.getAccessToken.mockResolvedValue(null);

    expect((await me(new Request('http://localhost'))).status).toBe(401);
    expect((await profile(new Request('http://localhost', { method: 'PUT' }))).status).toBe(401);
    expect((await password(new Request('http://localhost', { method: 'POST' }))).status).toBe(401);
  });

  it('proxies the current user request and preserves its response', async () => {
    vi.mocked(fetch).mockResolvedValue(response(200, '{"id":"user-1"}'));

    const result = await me(new Request('http://localhost'));

    expect(result.status).toBe(200);
    expect(await result.text()).toBe('{"id":"user-1"}');
    expect(vi.mocked(fetch)).toHaveBeenCalledWith('http://identity/api/v1/identity/me', expect.objectContaining({
      headers: { Authorization: 'Bearer access-token' },
    }));
  });

  it('proxies profile updates and password changes', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(response(200, '{"updated":true}'))
      .mockResolvedValueOnce(response(204, ''));

    const profileResult = await profile(new Request('http://localhost', { method: 'PUT', body: '{"firstName":"Jane"}' }));
    const passwordResult = await password(new Request('http://localhost', { method: 'POST', body: '{"currentPassword":"old"}' }));

    expect(await profileResult.json()).toEqual({ updated: true });
    expect(profileResult.status).toBe(200);
    expect(passwordResult.status).toBe(204);
    expect(vi.mocked(fetch)).toHaveBeenCalledTimes(2);
  });

  it('returns route errors when the identity backend or configuration fails', async () => {
    mocks.getIdentityUrl.mockImplementation(() => { throw new Error('missing config'); });
    expect((await me(new Request('http://localhost'))).status).toBe(500);
    expect((await profile(new Request('http://localhost', { method: 'PUT' }))).status).toBe(500);
    expect((await password(new Request('http://localhost', { method: 'POST' }))).status).toBe(500);

    mocks.getIdentityUrl.mockReturnValue('http://identity');
    vi.mocked(fetch).mockRejectedValue(new Error('identity unavailable'));
    expect((await me(new Request('http://localhost'))).status).toBe(500);
    expect((await profile(new Request('http://localhost', { method: 'PUT' }))).status).toBe(500);
    expect((await password(new Request('http://localhost', { method: 'POST' }))).status).toBe(500);
  });
});
