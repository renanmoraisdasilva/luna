import { accessTokenCookieName, decryptAccessToken } from './auth-cookie';
import { cookies } from 'next/headers';

export async function getAccessToken(): Promise<string | null> {
  const cookieValue = (await cookies()).get(accessTokenCookieName)?.value;

  if (!cookieValue) {
    return null;
  }

  return decryptAccessToken(cookieValue);
}

export function getIdentityUrl(): string {
  const identityUrl = process.env.IDENTITY_API_INTERNAL_URL;
  if (!identityUrl) {
    throw new Error('IDENTITY_API_INTERNAL_URL is not configured.');
  }

  return identityUrl;
}