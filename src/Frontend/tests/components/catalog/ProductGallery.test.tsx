import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ProductGallery from '../../../components/catalog/ProductGallery';

const images = [
  { imageUrl: 'second.jpg', altText: '', displayOrder: 2 },
  { imageUrl: 'first.jpg', altText: 'Front view', displayOrder: 1 },
];

describe('ProductGallery', () => {
  it('shows a placeholder when a product has no images', () => {
    render(<ProductGallery name="Keyboard" images={[]} />);

    expect(screen.getByText('No image')).toBeInTheDocument();
  });

  it('orders images and switches the selected image', () => {
    render(<ProductGallery name="Keyboard" images={images} />);

    expect(screen.getByRole('img', { name: 'Front view' })).toHaveAttribute('src', 'first.jpg');
    fireEvent.click(screen.getByRole('button', { name: 'View Keyboard' }));

    expect(screen.getByRole('img', { name: 'Keyboard' })).toHaveAttribute('src', 'second.jpg');
  });
});
