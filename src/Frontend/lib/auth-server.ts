import { accessTokenCookieName, decryptAccessToken } from './auth-cookie';
import { cookies } from 'next/headers';
import type { CurrentUser } from '../types/auth';

export async function getAccessToken(): Promise<string | null> {
  const cookieValue = (await cookies()).get(accessTokenCookieName)?.value;

  if (!cookieValue) {
    return null;
  }

  try {
    const accessToken = await decryptAccessToken(cookieValue);
    return accessToken;
  } catch {
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