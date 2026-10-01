import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SwaggerPage from '../../../app/swagger/page';
import { getCurrentUserServer } from '../../../lib/auth-server';

const mocks = vi.hoisted(() => ({
  redirect: vi.fn((url: string) => {
    throw new Error(`NEXT_REDIRECT:${url}`);
  }),
  notFound: vi.fn(() => {
    throw new Error('NEXT_NOT_FOUND');
  }),
}));

// Next.js redirect() and notFound() halt rendering by throwing. The mocks reproduce that so the assertions
// below observe the same control flow the real implementation produces.
async function renderPage() {
  try {
    return await SwaggerPage();
  } catch (error) {
    return undefined;
  }
}

vi.mock('next/navigation', () => ({ redirect: mocks.redirect, notFound: mocks.notFound }));
vi.mock('../../../lib/auth-server', () => ({ getCurrentUserServer: vi.fn() }));
vi.mock('../../../components/swagger/SwaggerExplorer', () => ({ default: () => <div>Explorer</div> }));

const admin = {
  id: 'admin-1',
  email: 'admin@example.com',
  firstName: 'Admin',
  lastName: 'User',
  roles: ['Admin'],
};

const customer = {
  id: 'customer-1',
  email: 'jane@example.com',
  firstName: 'Jane',
  lastName: 'Doe',
  roles: ['Customer'],
};

describe('SwaggerPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv('ENABLE_API_EXPLORER', 'true');
    vi.mocked(getCurrentUserServer).mockResolvedValue(admin);
  });

  it('renders the explorer for an administrator when it is enabled', async () => {
    const page = await renderPage();

    expect(mocks.notFound).not.toHaveBeenCalled();
    expect(mocks.redirect).not.toHaveBeenCalled();

    const { getByText } = render(page);
    expect(getByText('Explorer')).toBeDefined();
  });

  it('redirects anonymous visitors to sign in', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue(null);

    await renderPage();

    expect(mocks.redirect).toHaveBeenCalledWith('/login?returnUrl=/swagger');
  });

  it('redirects a signed-in customer away from the explorer', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue(customer);

    await renderPage();

    expect(mocks.redirect).toHaveBeenCalledWith('/shop');
  });

  it('hides the explorer entirely when it is not enabled', async () => {
    vi.stubEnv('ENABLE_API_EXPLORER', 'false');

    await renderPage();

    expect(mocks.notFound).toHaveBeenCalled();
    // The user is never even looked up, so an unauthenticated caller learns nothing about the page.
    expect(getCurrentUserServer).not.toHaveBeenCalled();
    expect(mocks.redirect).not.toHaveBeenCalled();
  });

  it('hides the explorer when the flag is absent', async () => {
    vi.stubEnv('ENABLE_API_EXPLORER', '');

    await renderPage();

    expect(mocks.notFound).toHaveBeenCalled();
  });
});