import { render, screen } from '@testing-library/react';
import Header from '../../../components/layout/Header';

describe('Header', () => {
  it('renders the storefront navigation links', () => {
    render(<Header />);

    expect(screen.getByRole('link', { name: 'Luna' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Shop' })).toHaveAttribute('href', '/shop');
    expect(screen.getByRole('link', { name: 'Orders' })).toHaveAttribute('href', '/orders');
    expect(screen.getByRole('link', { name: 'Account' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'Cart' })).toHaveAttribute('href', '/cart');
  });
});
