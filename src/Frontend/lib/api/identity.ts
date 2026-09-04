import axios from 'axios';

const identityBaseUrl = typeof window === 'undefined'
  ? `${process.env.IDENTITY_API_INTERNAL_URL}/api/v1/identity`
  : '/api/services/identity';

export const identityApi = axios.create({
  baseURL: identityBaseUrl,
  headers: { Accept: 'application/json' },
  withCredentials: true,
});

export type LoginCredentials = {
  email: string;
  password: string;
};

export type RegisterCredentials = LoginCredentials & {
  firstName: string;
  lastName: string;
};

export async function login(credentials: LoginCredentials): Promise<void> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(credentials),
  });

  if (!response.ok) {
    throw new Error('Unable to sign in.');
  }
}

export async function registerAccount(credentials: RegisterCredentials): Promise<void> {
  await identityApi.post('/register-profile', credentials);
}
