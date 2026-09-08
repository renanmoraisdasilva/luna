// @vitest-environment node

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { decryptAccessToken, encryptAccessToken } from '../../lib/auth-cookie';

describe('access token cookie encryption', () => {
  beforeEach(() => {
    vi.unstubAllEnvs();
  });

  it('requires an encryption key', async () => {
    await expect(encryptAccessToken('token')).rejects.toThrow('LUNA_COOKIE_ENCRYPTION_KEY is not configured.');
  });

  it('requires a 32-byte encryption key', async () => {
    vi.stubEnv('LUNA_COOKIE_ENCRYPTION_KEY', btoa('short'));

    await expect(encryptAccessToken('token')).rejects.toThrow('LUNA_COOKIE_ENCRYPTION_KEY must decode to 32 bytes.');
  });

  it('round-trips an access token', async () => {
    vi.stubEnv('LUNA_COOKIE_ENCRYPTION_KEY', btoa('12345678901234567890123456789012'));

    const encrypted = await encryptAccessToken('access-token');
    await expect(decryptAccessToken(encrypted)).resolves.toBe('access-token');
    await expect(decryptAccessToken(`${encrypted}invalid`)).rejects.toThrow();
  });
});
