import { NextRequest, NextResponse } from 'next/server';
import { accessTokenCookieName, decryptAccessToken } from './lib/auth-cookie';

export async function proxy(request: NextRequest) {
  const encryptedToken = request.cookies.get(accessTokenCookieName)?.value;
  if (!encryptedToken) {
    if (request.nextUrl.pathname === '/account' || request.nextUrl.pathname.startsWith('/account/')) {
      const loginUrl = new URL('/login', request.url);
      loginUrl.searchParams.set('returnUrl', request.nextUrl.pathname);
      return NextResponse.redirect(loginUrl);
    }

    return NextResponse.next();
  }

  try {
    const accessToken = await decryptAccessToken(encryptedToken);
    const requestHeaders = new Headers(request.headers);
    requestHeaders.set('Authorization', `Bearer ${accessToken}`);
    requestHeaders.delete('cookie');

    return NextResponse.next({ request: { headers: requestHeaders } });
  } catch {
    return NextResponse.next();
  }
}

export const config = {
  matcher: ['/api/services/:path*', '/account/:path*'],
};