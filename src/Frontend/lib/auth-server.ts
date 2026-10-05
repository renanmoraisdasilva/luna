import { accessTokenCookieName, decryptAccessToken } from './auth-cookie';
import { cookies } from 'next/headers';
import type { CurrentUser } from '../types/auth';

export async function getAccessToken(): Promise<string | null> {
  const name = accessTokenCookieName();
  const cookieValue = (await cookies()).get(name)?.value;

  if (!cookieValue) {
    return null;
  }

  try {
    return await decryptAccessToken(cookieValue);
  } catch (error) {
    // Logged rather than swallowed. A failed decryption is otherwise
    // indistinguishable from an absent cookie, so a wrong
    // LUNA_COOKIE_ENCRYPTION_KEY, a rotated key, or a truncated cookie all
    // surface as an unexplained 401 with no server-side record of why.
    console.error(
      `[auth] Failed to decrypt the access token cookie "${name}". ` +
        'This usually means LUNA_COOKIE_ENCRYPTION_KEY changed or differs between instances, ' +
        'which invalidates every existing session.',
      error,
    );
    return null;
  }
}

export function getIdentityUrl(): string {
  const identityUrl = process.env.IDENTITY_API_INTERNAL_URL;
  if (!identityUrl) {
    throw new Error('IDENTITY_API_INTERNAL_URL is not configured.');
  }

  return identityUrl;
}

export async function getCurrentUserServer(): Promise<CurrentUser | null> {
  const accessToken = await getAccessToken();
  if (!accessToken) {
    return null;
  }

  const response = await fetch(`${getIdentityUrl()}/api/v1/identity/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    cache: 'no-store',
  });

  if (response.status === 401) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Unable to load the current user.');
  }

  return response.json() as Promise<CurrentUser>;
}