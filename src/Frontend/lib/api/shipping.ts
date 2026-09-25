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

export type ShipmentStatus = 'Created' | 'InTransit' | 'Delivered';

export type ShipmentSummary = {
  shipmentId: string;
  orderId: string;
  customerId: string;
  customerName: string;
  destination: string;
  trackingNumber: string;
  status: ShipmentStatus;
  createdAt: string;
  inTransitAt?: string | null;
  deliveredAt?: string | null;
  availableAction: 'MarkInTransit' | 'MarkDelivered' | 'None';
};

export type ShipmentRecipient = {
  customerId: string;
  fullName: string;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  stateOrProvince: string;
  postalCode: string;
  country: string;
};

export type ShipmentTrackingEvent = {
  id: string;
  status: ShipmentStatus;
  occurredAt: string;
};

export type ShipmentDetail = {
  shipmentId: string;
  orderId: string;
  customerId: string;
  trackingNumber: string;
  status: ShipmentStatus;
  createdAt: string;
  inTransitAt?: string | null;
  deliveredAt?: string | null;
  recipient: ShipmentRecipient;
  trackingEvents: ShipmentTrackingEvent[];
  availableAction: 'MarkInTransit' | 'MarkDelivered' | 'None';
};

export type ShipmentListResponse = {
  items: ShipmentSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type ShipmentListParams = {
  status?: ShipmentStatus;
  search?: string;
  page: number;
  pageSize: number;
};

export async function getShippingMethods(): Promise<ShippingMethod[]> {
  const response = await shippingApi.get<ShippingMethodsResponse>('/shipping-methods');
  return response.data.methods;
}

export async function getShipments(params: ShipmentListParams): Promise<ShipmentListResponse> {
  const response = await shippingApi.get<ShipmentListResponse>('/shipments', {
    params: {
      status: params.status,
      search: params.search || undefined,
      page: params.page,
      pageSize: params.pageSize,
    },
  });

  return response.data;
}

export async function getShipment(shipmentId: string): Promise<ShipmentDetail> {
  const response = await shippingApi.get<ShipmentDetail>(`/shipments/${shipmentId}`);
  return response.data;
}
