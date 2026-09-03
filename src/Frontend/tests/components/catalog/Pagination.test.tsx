import { render, screen } from '@testing-library/react';
import Pagination from '../../../components/catalog/Pagination';

describe('Pagination', () => {
  it('renders the current page and navigation controls', () => {
    render(<Pagination page={2} pageSize={12} totalCount={36} pathname="/shop" currentQuery="category=electronics&page=2" />);

    expect(screen.getByRole('navigation', { name: 'Pagination' })).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Previous' })).toHaveAttribute('href', '/shop?category=electronics&page=1');
    expect(screen.getByRole('link', { name: 'Next' })).toHaveAttribute('href', '/shop?category=electronics&page=3');
  });
});
