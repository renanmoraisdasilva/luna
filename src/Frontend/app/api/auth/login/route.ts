import { NextResponse } from 'next/server';
import { accessTokenCookieName, encryptAccessToken } from '../../../../lib/auth-cookie';

// The storefront is a public OpenIddict client. It cannot keep a secret, so it authenticates with the
// password grant only. The identifier must match LunaPublicClients.Storefront in Luna.Contracts.
const storefrontClientId = 'luna-storefront';

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
    client_id: storefrontClientId,
    // Roles are read from the role claim, which the token endpoint copies from the account, so the storefront
    // does not need the roles scope. Requesting scopes the public client is not granted is rejected outright.
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
  const secureCookie = process.env.AUTH_COOKIE_SECURE === 'true';
  response.cookies.set({
    name: accessTokenCookieName,
    value: encryptedToken,
    httpOnly: true,
    secure: secureCookie,
    sameSite: 'lax',
    path: '/',
    maxAge: token.expires_in ?? 3600,
  });
  return response;
}
