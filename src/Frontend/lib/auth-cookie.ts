import { compactDecrypt, CompactEncrypt } from 'jose';

function resolveCookieName(): string {
  // The __Host- prefix is a promise that the cookie is Secure, sent to exactly this host, with Path=/ and no
  // Domain. A browser enforces the Secure half by refusing the cookie on any non-HTTPS origin other than
  // localhost, so prefixing a cookie that is not actually secure logs the user out silently: the login
  // response sets a cookie the browser discards, and every later request arrives without a token.
  // Keying the name off isSecureCookieRequired() keeps the prefix and the Secure attribute in agreement.
  return isSecureCookieRequired() ? '__Host-luna_access_token' : 'luna_access_token';
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

export function isSecureCookieRequired(): boolean {
  // AUTH_COOKIE_SECURE=false is honoured in production as well as in development. It is the operator stating
  // that this deployment terminates TLS somewhere the application cannot see, or that it is a local-only
  // build reached over plain HTTP. The cookie name follows this same value, so the two cannot disagree:
  // a Secure cookie always carries the __Host- prefix, and a plain one never does.
  return process.env.AUTH_COOKIE_SECURE !== 'false';
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