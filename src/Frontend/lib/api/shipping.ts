import axios from 'axios';

export const shippingApi = axios.create({
  baseURL: '/api/services/shipping',
  headers: { Accept: 'application/json' },
});

export type ShippingMethod = {
  id: string;
  code: string;
  name: string;
  cost: number;
  estimatedDeliveryDays: number;
};

export type ShippingMethodsResponse = {
  methods: ShippingMethod[];
};

export async function getShippingMethods(): Promise<ShippingMethod[]> {
  const response = await shippingApi.get<ShippingMethodsResponse>('/shipping-methods');
  return response.data.methods;
}
