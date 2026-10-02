import { compactDecrypt, CompactEncrypt } from 'jose';

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