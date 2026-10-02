import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  accessTokenCookieName,
  isSecureCookieRequired,
} from '../../lib/auth-cookie';

describe('session cookie configuration', () => {
  beforeEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  describe('accessTokenCookieName', () => {
    it('uses the __Host- prefix in production', async () => {
      vi.stubEnv('NODE_ENV', 'production');

      const { accessTokenCookieName: name } = await import('../../lib/auth-cookie');

      // The browser rejects a __Host- cookie that is not Secure, sent to exactly this host, with Path=/ and no
      // Domain. The prefix makes those three mistakes impossible rather than merely discouraged.
      expect(name()).toBe('__Host-luna_access_token');
    });

    it('omits the prefix outside production, because the prefix requires HTTPS', async () => {
      vi.stubEnv('NODE_ENV', 'development');

      const { accessTokenCookieName: name } = await import('../../lib/auth-cookie');

      expect(name()).toBe('luna_access_token');
    });
  });

  describe('isSecureCookieRequired', () => {
    it('is required in production regardless of the configured value', async () => {
      vi.stubEnv('NODE_ENV', 'production');
      vi.stubEnv('AUTH_COOKIE_SECURE', 'false');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      // AUTH_COOKIE_SECURE=false is accepted as a value, but it cannot turn off Secure in production. This is
      // the floor that stops an operator copying the example env file into production.
      expect(secure()).toBe(true);
    });

    it('is required in production when the variable is absent entirely', async () => {
      vi.stubEnv('NODE_ENV', 'production');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      expect(secure()).toBe(true);
    });

    it('defaults to required outside production', async () => {
      vi.stubEnv('NODE_ENV', 'development');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      expect(secure()).toBe(true);
    });

    it('can be turned off outside production for plain HTTP local development', async () => {
      vi.stubEnv('NODE_ENV', 'development');
      vi.stubEnv('AUTH_COOKIE_SECURE', 'false');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      expect(secure()).toBe(false);
    });
  });
});

// The statically imported bindings are exercised so the module is not tree-shaken out of this file.
expect(typeof accessTokenCookieName).toBe('function');
expect(typeof isSecureCookieRequired).toBe('function');