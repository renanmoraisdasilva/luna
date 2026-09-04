import { NextResponse } from 'next/server';
import { accessTokenCookieName, encryptAccessToken } from '../../../../lib/auth-cookie';

export async function POST(request: Request) {
  const identityBaseUrl = process.env.IDENTITY_API_INTERNAL_URL;
  if (!identityBaseUrl) {
    return NextResponse.json(
      { message: 'Identity API is not configured.' },
      { status: 500 },
    );
  }

  const credentials = await request.json() as { email?: string; password?: string };
  const form = new URLSearchParams({
    grant_type: 'password',
    username: credentials.email ?? '',
    password: credentials.password ?? '',
    scope: 'openid',
  });

  const tokenResponse = await fetch(`${identityBaseUrl}/api/v1/identity/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: form,
    cache: 'no-store',
  });

  if (!tokenResponse.ok) {
    return new NextResponse(await tokenResponse.text(), {
      status: tokenResponse.status,
      headers: { 'Content-Type': tokenResponse.headers.get('Content-Type') ?? 'application/json' },
    });
  }

  const token = await tokenResponse.json() as { access_token?: string; expires_in?: number };
  if (!token.access_token) {
    return NextResponse.json(
      { message: 'Identity did not return an access token.' },
      { status: 502 },
    );
  }

  const encryptedToken = await encryptAccessToken(token.access_token);
  const response = NextResponse.json({ authenticated: true });
  response.cookies.set({
    name: accessTokenCookieName,
    value: encryptedToken,
    httpOnly: true,
    secure: process.env.AUTH_COOKIE_SECURE === 'true',
    sameSite: 'lax',
    path: '/',
    maxAge: token.expires_in ?? 3600,
  });
  return response;
}