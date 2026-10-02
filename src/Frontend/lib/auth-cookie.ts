import { compactDecrypt, CompactEncrypt } from 'jose';

/**
 * Name of the session cookie.
 *
 * The `__Host-` prefix is enforced by the browser and can only be used on a secure cookie that is sent to
 * exactly this host, over HTTPS, with no `Domain` attribute. A browser rejects the cookie outright if any of
 * those conditions is violated, which turns three cookie-scoping mistakes — a missing `Secure` flag, a
 * narrower `Path`, or a `Domain` that widens the scope to a parent registrable domain — from silent
 * vulnerabilities into a cookie the browser throws away. That matters because this cookie carries the
 * encrypted access token.
 *
 * The prefix requires HTTPS, so local development over plain HTTP cannot use it. The name therefore switches
 * on the environment: prefixed outside development, unprefixed in development.
 */
function resolveCookieName(): string {
  return process.env.NODE_ENV === 'production' ? '__Host-luna_access_token' : 'luna_access_token';
}

function getEncryptionKey(): Uint8Array {
  const encodedKey = process.env.LUNA_COOKIE_ENCRYPTION_KEY;
  if (!encodedKey) {
    throw new Error('LUNA_COOKIE_ENCRYPTION_KEY is not configured.');
  }

  const key = Uint8Array.from(atob(encodedKey), character => character.charCodeAt(0));
  if (key.length !== 32) {
    throw new Error('LUNA_COOKIE_ENCRYPTION_KEY must decode to 32 bytes.');
  }

  return key;
}

/**
 * Whether the session cookie must carry the `Secure` flag.
 *
 * In production the cookie is always secure. `AUTH_COOKIE_SECURE` exists for environments that terminate TLS
 * elsewhere, and defaults to secure rather than insecure, so a missing or miscopied variable cannot silently
 * produce a session cookie that travels in clear text.
 */
export function isSecureCookieRequired(): boolean {
  return process.env.NODE_ENV === 'production' || process.env.AUTH_COOKIE_SECURE !== 'false';
}

export async function encryptAccessToken(token: string): Promise<string> {
  return new CompactEncrypt(new TextEncoder().encode(token))
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .encrypt(getEncryptionKey());
}

export async function decryptAccessToken(value: string): Promise<string> {
  const { plaintext } = await compactDecrypt(value, getEncryptionKey());
  return new TextDecoder().decode(plaintext);
}

export { resolveCookieName as accessTokenCookieName };