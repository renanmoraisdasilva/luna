import { NextResponse } from 'next/server';
import { getAccessToken, getIdentityUrl } from '../../../../lib/auth-server';

export async function POST(request: Request) {
  try {
    const accessToken = await getAccessToken();
    if (!accessToken) {
      return NextResponse.json({ message: 'Not authenticated.' }, { status: 401 });
    }

    const response = await fetch(`${getIdentityUrl()}/api/v1/identity/me/change-password`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      body: await request.text(),
      cache: 'no-store',
    });

    if (response.status === 204) {
      return new NextResponse(null, { status: 204 });
    }

    return new NextResponse(await response.text(), {
      status: response.status,
      headers: { 'Content-Type': response.headers.get('Content-Type') ?? 'application/json' },
    });
  } catch {
    return NextResponse.json({ message: 'Unable to change the password.' }, { status: 500 });
  }
}