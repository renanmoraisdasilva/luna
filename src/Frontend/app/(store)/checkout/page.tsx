import { redirect } from 'next/navigation';
import CheckoutForm from '../../../components/checkout/CheckoutForm';
import Footer from '../../../components/layout/Footer';
import { getAccessToken, getCurrentUserServer } from '../../../lib/auth-server';
import type { Cart } from '../../../lib/api/orders';
import type { ShippingMethod, ShippingMethodsResponse } from '../../../lib/api/shipping';
import type { CatalogProduct } from '../../../types/catalog';

type CheckoutData = {
  cart: Cart;
  products: Record<string, CatalogProduct | null>;
  shippingMethods: ShippingMethod[];
};

async function getCheckoutData(): Promise<CheckoutData> {
  const accessToken = await getAccessToken();
  const ordersUrl = process.env.ORDERS_API_INTERNAL_URL;
  const catalogUrl = process.env.CATALOG_API_INTERNAL_URL;
  const shippingUrl = process.env.SHIPPING_API_INTERNAL_URL;
  if (!accessToken || !ordersUrl || !catalogUrl || !shippingUrl) {
    throw new Error('Checkout services are not configured.');
  }

  const headers = { Authorization: `Bearer ${accessToken}`, Accept: 'application/json' };
  const cartResponse = await fetch(`${ordersUrl}/api/v1/orders/cart`, { headers, cache: 'no-store' });
  if (!cartResponse.ok) {
    throw new Error('Unable to load the cart.');
  }

  const cart = await cartResponse.json() as Cart;
  const [productResults, shippingResponse] = await Promise.all([
    Promise.all(cart.items.map(async (item) => {
      const response = await fetch(`${catalogUrl}/api/v1/catalog/products/${item.productId}`, { headers, cache: 'no-store' });
      return [item.productId, response.ok ? await response.json() as CatalogProduct : null] as const;
    })),
    fetch(`${shippingUrl}/api/v1/shipping-methods`, { headers, cache: 'no-store' }),
  ]);

  if (!shippingResponse.ok) {
    throw new Error('Unable to load shipping methods.');
  }

  const shippingData = await shippingResponse.json() as ShippingMethodsResponse;
  return { cart, products: Object.fromEntries(productResults), shippingMethods: shippingData.methods };
}

export default async function CheckoutPage() {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) {
    redirect('/login?returnUrl=/checkout');
  }

  const { cart, products, shippingMethods } = await getCheckoutData();
  return (
    <>
      <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
        <div>
          <p className="font-label-caps text-label-caps uppercase tracking-widest text-secondary">Secure checkout</p>
          <h1 className="mt-sm font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">Complete your order</h1>
        </div>
        <CheckoutForm cart={cart} products={products} shippingMethods={shippingMethods} email={currentUser.email} />
      </main>
      <Footer />
    </>
  );
}
