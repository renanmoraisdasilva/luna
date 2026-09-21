import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FulfillmentPage from '../../../../app/operations/fulfillment/page';
import { getCurrentUserServer } from '../../../../lib/auth-server';

const mocks = vi.hoisted(() => ({ redirect: vi.fn() }));

vi.mock('next/navigation', () => ({ redirect: mocks.redirect }));
vi.mock('../../../../lib/auth-server', () => ({ getCurrentUserServer: vi.fn() }));
vi.mock('../../../../components/operations/FulfillmentQueue', () => ({ default: () => <div>Fulfillment queue</div> }));
vi.mock('../../../../components/layout/Footer', () => ({ default: () => <footer>Footer</footer> }));

describe('FulfillmentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('redirects authenticated non-admin users away from operations', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue({
      id: 'customer-1',
      email: 'jane@example.com',
      firstName: 'Jane',
      lastName: 'Doe',
      roles: ['Customer'],
    });

    await FulfillmentPage();

    expect(mocks.redirect).toHaveBeenCalledWith('/shop');
  });

  it('renders the fulfillment page for admins', async () => {
    vi.mocked(getCurrentUserServer).mockResolvedValue({
      id: 'admin-1',
      email: 'admin@example.com',
      firstName: 'Admin',
      lastName: 'User',
      roles: ['Admin'],
    });

    const page = await FulfillmentPage();
    render(page);

    expect(mocks.redirect).not.toHaveBeenCalled();
  });
});
