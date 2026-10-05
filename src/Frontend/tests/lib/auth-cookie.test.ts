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
    it('uses the __Host- prefix when the cookie is secure', async () => {
      vi.stubEnv('NODE_ENV', 'production');

      const { accessTokenCookieName: name } = await import('../../lib/auth-cookie');

      // The browser rejects a __Host- cookie that is not Secure, sent to exactly this host, with Path=/ and no
      // Domain. The prefix makes those three mistakes impossible rather than merely discouraged.
      expect(name()).toBe('__Host-luna_access_token');
    });

    it('keeps the prefix outside production when Secure is left at its default', async () => {
      vi.stubEnv('NODE_ENV', 'development');

      const { accessTokenCookieName: name, isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      // Unset is not 'false', so development defaults to a secure cookie and the prefix with it.
      expect(secure()).toBe(true);
      expect(name()).toBe('__Host-luna_access_token');
    });

    it('omits the prefix in production when Secure is turned off', async () => {
      vi.stubEnv('NODE_ENV', 'production');
      vi.stubEnv('AUTH_COOKIE_SECURE', 'false');

      const { accessTokenCookieName: name, isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      // The regression this pins: a browser refuses a __Host- cookie on any non-HTTPS origin other than
      // localhost, so prefixing a non-secure cookie logs the user out silently. The name and the Secure
      // attribute are now derived from one value and cannot disagree.
      expect(secure()).toBe(false);
      expect(name()).toBe('luna_access_token');
    });
  });

  describe('isSecureCookieRequired', () => {
    it('can be turned off in production for a plain-HTTP local deployment', async () => {
      vi.stubEnv('NODE_ENV', 'production');
      vi.stubEnv('AUTH_COOKIE_SECURE', 'false');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      // Honoured in production as well as development. The operator is stating that TLS terminates
      // somewhere the application cannot see, or that this build is reached over plain HTTP.
      expect(secure()).toBe(false);
    });

    it('is required in production when the variable is absent entirely', async () => {
      vi.stubEnv('NODE_ENV', 'production');

      const { isSecureCookieRequired: secure } = await import('../../lib/auth-cookie');

      // Absent means unset, which is not 'false', so Secure stays on. Copying an env file that omits
      // the variable still gets a hardened cookie.
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