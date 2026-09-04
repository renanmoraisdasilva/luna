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

export async function login(credentials: LoginCredentials): Promise<void> {
  await identityApi.post('/login?useCookies=true', credentials);
}
