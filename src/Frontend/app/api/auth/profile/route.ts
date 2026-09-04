import { NextResponse } from 'next/server';
import { getAccessToken, getIdentityUrl } from '../../../../lib/auth-server';

export async function PUT(request: Request) {
  try {
    const accessToken = await getAccessToken();
    if (!accessToken) {
      return NextResponse.json({ message: 'Not authenticated.' }, { status: 401 });
    }

    const response = await fetch(`${getIdentityUrl()}/api/v1/identity/me`, {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      body: await request.text(),
      cache: 'no-store',
    });

    return new NextResponse(await response.text(), {
      status: response.status,
      headers: { 'Content-Type': response.headers.get('Content-Type') ?? 'application/json' },
    });
  } catch {
    return NextResponse.json({ message: 'Unable to update the profile.' }, { status: 500 });
  }
}