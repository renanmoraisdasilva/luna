import { NextResponse } from 'next/server';
import { getAccessToken, getIdentityUrl } from '../../../../lib/auth-server';

export async function GET(request: Request) {
  try {
    const accessToken = await getAccessToken();
    if (!accessToken) {
      return NextResponse.json({ message: 'Not authenticated.' }, { status: 401 });
    }

    const response = await fetch(`${getIdentityUrl()}/api/v1/identity/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: 'no-store',
    });

    return new NextResponse(await response.text(), {
      status: response.status,
      headers: { 'Content-Type': response.headers.get('Content-Type') ?? 'application/json' },
    });
  } catch {
    return NextResponse.json({ message: 'Unable to load the current user.' }, { status: 500 });
  }
}