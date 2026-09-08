import axios from 'axios';

export const ordersApi = axios.create({
  baseURL: '/api/services/orders',
  headers: { Accept: 'application/json' },
});

export type CartItem = {
  productId: string;
  quantity: number;
};

export type Cart = {
  id: string;
  customerId: string;
  items: CartItem[];
};

export type AddCartItemRequest = {
  productId: string;
  quantity: number;
};

export async function addCartItem(request: AddCartItemRequest): Promise<Cart> {
  const response = await ordersApi.post<Cart>('/cart/items', request);
  return response.data;
}

export async function getCart(): Promise<Cart> {
  const response = await ordersApi.get<Cart>('/cart');
  return response.data;
}

export async function changeCartItemQuantity(productId: string, quantity: number): Promise<Cart> {
  const response = await ordersApi.put<Cart>(`/cart/items/${productId}`, { quantity });
  return response.data;
}

export async function removeCartItem(productId: string): Promise<void> {
  await ordersApi.delete(`/cart/items/${productId}`);
}
