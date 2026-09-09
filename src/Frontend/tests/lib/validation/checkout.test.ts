import { describe, expect, it } from 'vitest';
import { checkoutSchema } from '../../../lib/validation/checkout';

const validCheckout = {
  email: 'jane@example.com',
  fullName: 'Jane Doe',
  addressLine1: '123 Luna Street',
  addressLine2: '',
  city: 'Austin',
  state: 'Texas',
  postalCode: '78701',
  country: 'United States',
  shippingMethodCode: 'STANDARD',
  paymentMethod: 'test-card',
};

describe('checkoutSchema', () => {
  it('accepts a complete checkout form', () => {
    expect(checkoutSchema.safeParse(validCheckout).success).toBe(true);
  });

  it('requires contact, address, and shipping details', () => {
    const result = checkoutSchema.safeParse({ ...validCheckout, email: 'invalid', addressLine1: '', shippingMethodCode: '', paymentMethod: '' });

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.map((issue) => issue.path.join('.'))).toEqual(expect.arrayContaining(['email', 'addressLine1', 'shippingMethodCode', 'paymentMethod']));
    }
  });
});
