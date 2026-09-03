import { render, screen } from '@testing-library/react';
import Pagination from '../../../components/catalog/Pagination';

describe('Pagination', () => {
  it('renders the current page and navigation controls', () => {
    render(<Pagination />);

    expect(screen.getByRole('navigation', { name: 'Pagination' })).toBeInTheDocument();
    expect(screen.getByText('Page 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeInTheDocument();
  });
});
