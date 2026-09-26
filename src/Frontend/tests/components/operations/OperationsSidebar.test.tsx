import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import OperationsSidebar from '../../../components/operations/OperationsSidebar';

vi.mock('next/navigation', () => ({
  usePathname: () => '/operations/fulfillment',
}));

describe('OperationsSidebar', () => {
  it('places Swagger and SigNoz in the developer tools menu', () => {
    render(<OperationsSidebar />);

    const swaggerLinks = screen.getAllByRole('link', { name: 'Swagger' });
    const signozLinks = screen.getAllByRole('link', { name: 'SigNoz' });
    expect(swaggerLinks).toHaveLength(2);
    expect(signozLinks).toHaveLength(2);
    expect(swaggerLinks[0]).toHaveAttribute('href', '/swagger');
    expect(signozLinks[0]).toHaveAttribute('href', 'http://localhost:8080');
    expect(signozLinks[0]).toHaveAttribute('target', '_blank');
    expect(screen.getAllByRole('link', { name: 'Fulfillment' })[0]).toHaveAttribute('aria-current', 'page');
  });
});
