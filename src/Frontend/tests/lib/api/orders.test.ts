import { vi } from 'vitest';
import { addCartItem, changeCartItemQuantity, getCart, ordersApi, removeCartItem, submitCheckout } from '../../../lib/api/orders';

describe('Orders API client', () => {
  it('supports reading and mutating the cart', async () => {
    const cart = { id: 'cart-1', customerId: 'customer-1', items: [] };
    const get = vi.spyOn(ordersApi, 'get').mockResolvedValueOnce({ data: cart });
    const post = vi.spyOn(ordersApi, 'post').mockResolvedValueOnce({ data: cart });
    const put = vi.spyOn(ordersApi, 'put').mockResolvedValueOnce({ data: cart });
    const del = vi.spyOn(ordersApi, 'delete').mockResolvedValueOnce({ data: null });

    await expect(getCart()).resolves.toEqual(cart);
    await expect(addCartItem({ productId: 'product-1', quantity: 2 })).resolves.toEqual(cart);
    await expect(changeCartItemQuantity('product-1', 3)).resolves.toEqual(cart);
    await expect(removeCartItem('product-1')).resolves.toBeUndefined();

    expect(get).toHaveBeenCalledWith('/cart');
    expect(post).toHaveBeenCalledWith('/cart/items', { productId: 'product-1', quantity: 2 });
    expect(put).toHaveBeenCalledWith('/cart/items/product-1', { quantity: 3 });
    expect(del).toHaveBeenCalledWith('/cart/items/product-1');
  });

  it('propagates cart API failures to the caller', async () => {
    vi.spyOn(ordersApi, 'get').mockRejectedValueOnce(new Error('Orders unavailable.'));

    await expect(getCart()).rejects.toThrow('Orders unavailable.');
  });

  it('sends the caller-generated checkout idempotency key', async () => {
    const response = { data: { orderId: 'order-1' } };
    const post = vi.spyOn(ordersApi, 'post').mockResolvedValueOnce(response);
    const request = {
      fullName: 'Jane Doe',
      addressLine1: '123 Luna Street',
      city: 'Austin',
      stateOrProvince: 'Texas',
      postalCode: '78701',
      country: 'US',
      shippingMethodCode: 'STANDARD',
      paymentMethod: 'test-card',
      currency: 'USD',
    };

    await expect(submitCheckout(request, 'checkout-attempt-1')).resolves.toEqual(response.data);
    expect(post).toHaveBeenCalledWith('/checkout', request, { headers: { 'Idempotency-Key': 'checkout-attempt-1' } });
  });
});
