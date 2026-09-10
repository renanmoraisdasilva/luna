import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import PrintButton from '../../../components/orders/PrintButton';

describe('PrintButton', () => {
  it('prints the invoice when clicked', () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    render(<PrintButton />);
    fireEvent.click(screen.getByRole('button', { name: 'Download invoice' }));

    expect(print).toHaveBeenCalledOnce();
    print.mockRestore();
  });
});
