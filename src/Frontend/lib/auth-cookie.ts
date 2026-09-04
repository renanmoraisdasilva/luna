import { compactDecrypt, CompactEncrypt } from 'jose';

const cookieName = 'luna_access_token';

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

export async function encryptAccessToken(token: string): Promise<string> {
  return new CompactEncrypt(new TextEncoder().encode(token))
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .encrypt(getEncryptionKey());
}

export async function decryptAccessToken(value: string): Promise<string> {
  const { plaintext } = await compactDecrypt(value, getEncryptionKey());
  return new TextDecoder().decode(plaintext);
}

export { cookieName as accessTokenCookieName };