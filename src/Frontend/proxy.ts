import { NextRequest, NextResponse } from 'next/server';
import { accessTokenCookieName, decryptAccessToken } from './lib/auth-cookie';

const protectedPagePrefixes = ['/account', '/checkout', '/orders'];

function isProtectedPage(pathname: string): boolean {
  return protectedPagePrefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

export async function proxy(request: NextRequest) {
  const encryptedToken = request.cookies.get(accessTokenCookieName)?.value;
  if (!encryptedToken) {
    if (isProtectedPage(request.nextUrl.pathname)) {
      const loginUrl = new URL('/login', request.url);
      loginUrl.searchParams.set('returnUrl', `${request.nextUrl.pathname}${request.nextUrl.search}`);
      return NextResponse.redirect(loginUrl);
    }

    return NextResponse.next();
  }

  try {
    const accessToken = await decryptAccessToken(encryptedToken);

    if (!request.nextUrl.pathname.startsWith('/api/services/')) {
      return NextResponse.next();
    }

    const requestHeaders = new Headers(request.headers);
    requestHeaders.set('Authorization', `Bearer ${accessToken}`);
    requestHeaders.delete('cookie');

    return NextResponse.next({ request: { headers: requestHeaders } });
  } catch {
    return NextResponse.next();
  }
}

export const config = {
  matcher: ['/api/services/:path*', '/account/:path*', '/checkout/:path*', '/orders/:path*'],
};
