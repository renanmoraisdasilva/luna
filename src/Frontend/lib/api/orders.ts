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

export type CheckoutRequest = {
  fullName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateOrProvince: string;
  postalCode: string;
  country: string;
  shippingMethodCode: string;
  paymentMethod: string;
  currency: string;
};

export type CheckoutResponse = {
  orderId: string;
  status: string;
  total: number;
  currency: string;
  reservationId: string;
  paymentId: string;
};

export type OrderItem = {
  productId: string;
  sku: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type OrderSummary = {
  id: string;
  status: string;
  total: number;
  createdAt: string;
  itemCount: number;
};

export type Order = {
  id: string;
  customerId: string;
  status: string;
  subtotal: number;
  shippingCost: number;
  total: number;
  shippingMethodCode: string;
  createdAt: string;
  shippingAddress: {
    fullName: string;
    addressLine1: string;
    addressLine2?: string | null;
    city: string;
    stateOrProvince: string;
    postalCode: string;
    country: string;
  };
  items: OrderItem[];
};

export type FulfillmentStatus = 'Confirmed' | 'Preparing' | 'ShippingPendingRetry' | 'Shipped' | 'Delivered';

export type FulfillmentOrderSummary = {
  orderId: string;
  customerId: string;
  customerName: string;
  itemCount: number;
  total: number;
  paymentStatus: string;
  orderStatus: FulfillmentStatus;
  createdAt: string;
  availableAction: 'StartPreparing' | 'CreateShipment' | 'RetryShipment' | 'None';
};

export type FulfillmentOrder = {
  orderId: string;
  customerId: string;
  customerName: string;
  orderStatus: FulfillmentStatus;
  paymentStatus: string;
  subtotal: number;
  shippingCost: number;
  total: number;
  shippingMethodCode: string;
  createdAt: string;
  shippingAddress: Order['shippingAddress'];
  items: OrderItem[];
  availableAction: FulfillmentOrderSummary['availableAction'];
};

export type FulfillmentCommandResponse = {
  orderId: string;
  orderStatus: FulfillmentStatus;
};

export type FulfillmentQueueResponse = {
  items: FulfillmentOrderSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type FulfillmentQueueParams = {
  status?: FulfillmentStatus;
  search?: string;
  page: number;
  pageSize: number;
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

export async function submitCheckout(request: CheckoutRequest, idempotencyKey: string): Promise<CheckoutResponse> {
  const response = await ordersApi.post<CheckoutResponse>('/checkout', request, { headers: { 'Idempotency-Key': idempotencyKey } });
  return response.data;
}

export async function getOrder(id: string): Promise<Order> {
  const response = await ordersApi.get<Order>(`/${id}`);
  return response.data;
}

export async function getOrders(): Promise<OrderSummary[]> {
  const response = await ordersApi.get<OrderSummary[]>('/');
  return response.data;
}

export async function getFulfillmentQueue(params: FulfillmentQueueParams): Promise<FulfillmentQueueResponse> {
  const response = await ordersApi.get<FulfillmentQueueResponse>('/fulfillment', {
    params: {
      status: params.status,
      search: params.search || undefined,
      page: params.page,
      pageSize: params.pageSize,
    },
  });

  return response.data;
}

export async function prepareFulfillmentOrder(orderId: string): Promise<FulfillmentCommandResponse> {
  const response = await ordersApi.post<FulfillmentCommandResponse>(`/fulfillment/${orderId}/prepare`);
  return response.data;
}

export async function createShipment(orderId: string): Promise<FulfillmentCommandResponse> {
  const response = await ordersApi.post<FulfillmentCommandResponse>(`/fulfillment/${orderId}/shipment`);
  return response.data;
}

export async function changeCartItemQuantity(productId: string, quantity: number): Promise<Cart> {
  const response = await ordersApi.put<Cart>(`/cart/items/${productId}`, { quantity });
  return response.data;
}

export async function removeCartItem(productId: string): Promise<void> {
  await ordersApi.delete(`/cart/items/${productId}`);
}
