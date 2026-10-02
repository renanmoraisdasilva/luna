import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  cookies: vi.fn(),
  decryptAccessToken: vi.fn(),
}));

vi.mock('next/headers', () => ({ cookies: mocks.cookies }));
vi.mock('../../lib/auth-cookie', () => ({
  accessTokenCookieName: () => 'luna_access_token',
  isSecureCookieRequired: () => true,
  decryptAccessToken: mocks.decryptAccessToken,
}));

import { getAccessToken, getCurrentUserServer, getIdentityUrl } from '../../lib/auth-server';

describe('server authentication helpers', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    mocks.cookies.mockResolvedValue({ get: vi.fn().mockReturnValue(undefined) });
    mocks.decryptAccessToken.mockReset();
    vi.stubGlobal('fetch', vi.fn());
  });

  it('returns null for a missing or invalid encrypted cookie', async () => {
    expect(await getAccessToken()).toBeNull();

    mocks.cookies.mockResolvedValue({ get: vi.fn().mockReturnValue({ value: 'invalid' }) });
    mocks.decryptAccessToken.mockRejectedValue(new Error('invalid token'));

    expect(await getAccessToken()).toBeNull();
  });

  it('decrypts a valid cookie', async () => {
    mocks.cookies.mockResolvedValue({ get: vi.fn().mockReturnValue({ value: 'encrypted' }) });
    mocks.decryptAccessToken.mockResolvedValue('access-token');

    expect(await getAccessToken()).toBe('access-token');
  });

  it('requires the internal Identity URL', () => {
    expect(() => getIdentityUrl()).toThrow('IDENTITY_API_INTERNAL_URL is not configured.');
    vi.stubEnv('IDENTITY_API_INTERNAL_URL', 'http://identity');
    expect(getIdentityUrl()).toBe('http://identity');
  });

  it('returns null without a token and for an unauthorized Identity response', async () => {
    expect(await getCurrentUserServer()).toBeNull();

    mocks.cookies.mockResolvedValue({ get: vi.fn().mockReturnValue({ value: 'encrypted' }) });
    mocks.decryptAccessToken.mockResolvedValue('access-token');
    vi.stubEnv('IDENTITY_API_INTERNAL_URL', 'http://identity');
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 401 }));

    expect(await getCurrentUserServer()).toBeNull();
  });

  it('returns the current user or throws for an unexpected Identity response', async () => {
    mocks.cookies.mockResolvedValue({ get: vi.fn().mockReturnValue({ value: 'encrypted' }) });
    mocks.decryptAccessToken.mockResolvedValue('access-token');
    vi.stubEnv('IDENTITY_API_INTERNAL_URL', 'http://identity');
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ id: 'user-1' }), { status: 200 }));

    await expect(getCurrentUserServer()).resolves.toEqual({ id: 'user-1' });

    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 503 }));
    await expect(getCurrentUserServer()).rejects.toThrow('Unable to load the current user.');
  });
});
