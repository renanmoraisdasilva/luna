import { NextResponse } from 'next/server';
import { accessTokenCookieName } from '../../../../lib/auth-cookie';

export async function POST() {
  const response = NextResponse.json({ authenticated: false });
  response.cookies.delete(accessTokenCookieName);
  return response;
}