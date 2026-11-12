import type { ApiClient } from './client';
import type { CsrfMemory } from './csrf';
import { isApiError } from './errors';

export interface OptionPair {
  name: string;
  value: string;
}

export interface PostalAddress {
  recipient: string;
  line1: string;
  line2?: string | null;
  district: string;
  city: string;
  postalCode: string;
  country: string;
  notes?: string | null;
}

export interface CartLine {
  id: string;
  variantId: string;
  productName: string;
  sku: string;
  options: OptionPair[];
  unitPrice: string;
  quantity: number;
  lineTotal: string;
  availableQuantity: number;
  purchasable: boolean;
  unavailableReason: string | null;
  quantityLimited: boolean;
}

export interface Cart {
  id: string | null;
  itemCount: number;
  currency: string;
  items: CartLine[];
  expiresAt: string | null;
}

export interface QuoteLine {
  variantId: string;
  productName: string;
  sku: string;
  options: OptionPair[];
  unitPrice: string;
  quantity: number;
  lineTotal: string;
  purchasable: boolean;
  unavailableReason: string | null;
}

export interface Quote {
  currency: string;
  merchandiseTotal: string;
  shippingTotal: string;
  grandTotal: string;
  reservationExpiresInMinutes: number;
  shippingFee: string;
  freeShippingThreshold: string;
  purchasable: boolean;
  lines: QuoteLine[];
}

export interface OrderLine {
  variantId: string;
  productName: string;
  sku: string;
  options: OptionPair[];
  unitPrice: string;
  quantity: number;
  lineTotal: string;
}

export interface OrderAddress {
  kind: string;
  recipient: string;
  line1: string;
  line2: string | null;
  district: string;
  city: string;
  postalCode: string;
  country: string;
  notes: string | null;
}

export interface OrderHistoryEntry {
  fromStatus: string | null;
  toStatus: string;
  actor: string;
  reason: string | null;
  createdAt: string;
}

export interface Order {
  id: string;
  reference: string;
  status: string;
  email: string;
  phone: string;
  currency: string;
  merchandiseTotal: string;
  shippingTotal: string;
  grandTotal: string;
  expiresAt: string;
  createdAt: string;
  paymentCollected: boolean;
  reservationMinutes: number;
  items: OrderLine[];
  addresses: OrderAddress[];
  history: OrderHistoryEntry[];
}

export interface PlacedOrder {
  order: Order;
  accessToken: string;
}

export interface PlaceOrderInput {
  email: string;
  phone: string;
  lines: { variantId: string; quantity: number }[];
  shipping: PostalAddress;
  billingSameAsShipping: boolean;
  billing?: PostalAddress | null;
  expectedGrandTotal: string;
}

export interface AdminOrderRow {
  id: string;
  reference: string;
  status: string;
  email: string;
  grandTotal: string;
  createdAt: string;
  expiresAt: string;
}

export interface AdminOrderPage {
  items: AdminOrderRow[];
  page: number;
  pageSize: number;
  total: number;
  shippingFee: string;
  freeShippingThreshold: string;
  pendingPaymentMinutes: number;
}

export interface CommerceApi {
  cart: () => Promise<Cart>;
  addItem: (variantId: string, quantity: number) => Promise<Cart>;
  updateItem: (itemId: string, quantity: number) => Promise<Cart>;
  removeItem: (itemId: string) => Promise<Cart>;
  quote: () => Promise<Quote>;
  placeOrder: (idempotencyKey: string, input: PlaceOrderInput) => Promise<PlacedOrder>;
  orders: () => Promise<Order[]>;
  order: (reference: string) => Promise<Order>;
  orderByAccess: (token: string) => Promise<Order>;
  adminOrders: (query?: {
    status?: string;
    reference?: string;
    page?: number;
  }) => Promise<AdminOrderPage>;
  adminOrder: (orderId: string) => Promise<Order>;
  cancelOrder: (orderId: string, reason: string) => Promise<Order>;
}

const accessHeader = 'X-Order-Access';

export function createCommerceApi(client: ApiClient, csrf: CsrfMemory): CommerceApi {
  async function write<T>(run: () => Promise<T>): Promise<T> {
    await csrf.ensure(client);
    try {
      return await run();
    } catch (error) {
      if (
        !isApiError(error) ||
        error.status !== 400 ||
        error.title !== 'The request could not be validated.'
      ) {
        throw error;
      }
      csrf.clear();
      await csrf.ensure(client);
      return run();
    }
  }

  return {
    cart: () => client.get<Cart>('/api/v1/cart'),
    addItem: (variantId, quantity) =>
      write(() => client.post<Cart>('/api/v1/cart/items', { variantId, quantity })),
    updateItem: (itemId, quantity) =>
      write(() => client.patch<Cart>(`/api/v1/cart/items/${itemId}`, { quantity })),
    removeItem: (itemId) => write(() => client.delete<Cart>(`/api/v1/cart/items/${itemId}`)),
    quote: () => write(() => client.post<Quote>('/api/v1/checkout/quote', {})),
    placeOrder: (idempotencyKey, input) =>
      write(() =>
        client.post<PlacedOrder>('/api/v1/orders', input, {
          headers: { 'Idempotency-Key': idempotencyKey },
        }),
      ),
    orders: () => client.get<Order[]>('/api/v1/orders'),
    order: (reference) => client.get<Order>(`/api/v1/orders/${encodeURIComponent(reference)}`),
    orderByAccess: (token) =>
      client.get<Order>('/api/v1/orders/access', { headers: { [accessHeader]: token } }),
    adminOrders: (query) => client.get<AdminOrderPage>('/api/v1/admin/orders', { query }),
    adminOrder: (orderId) => client.get<Order>(`/api/v1/admin/orders/${orderId}`),
    cancelOrder: (orderId, reason) =>
      write(() => client.post<Order>(`/api/v1/admin/orders/${orderId}/cancel`, { reason })),
  };
}
