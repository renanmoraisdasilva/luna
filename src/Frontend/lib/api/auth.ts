import type { CurrentUser } from '../../types/auth';

async function readResponse(response: Response): Promise<CurrentUser | null> {
  if (response.status === 401) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Authentication request failed.');
  }

  return response.json() as Promise<CurrentUser>;
}

export async function getCurrentUser(): Promise<CurrentUser | null> {
  const response = await fetch('/api/auth/me', { cache: 'no-store', credentials: 'include' });
  return readResponse(response);
}

export async function updateProfile(profile: Pick<CurrentUser, 'firstName' | 'lastName'>): Promise<CurrentUser> {
  const response = await fetch('/api/auth/profile', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(profile),
  });

  if (!response.ok) {
    throw new Error('Profile update failed.');
  }

  return response.json() as Promise<CurrentUser>;
}

export async function logout(): Promise<void> {
  const response = await fetch('/api/auth/logout', { method: 'POST' });
  if (!response.ok) {
    throw new Error('Logout failed.');
  }
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const response = await fetch('/api/auth/password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ currentPassword, newPassword }),
  });

  if (!response.ok) {
    throw new Error('Password change failed.');
  }
}