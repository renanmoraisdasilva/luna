import Image from 'next/image';
import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import Footer from '../../../../components/layout/Footer';
import { getCurrentUserServer } from '../../../../lib/auth-server';
import { getOrderById, getOrderProducts } from '../../../../lib/server/orders';
import { formatCurrency } from '../../../../lib/formatters/currency';

type ConfirmationPageProps = {
  searchParams: Promise<{ orderId?: string }>;
};

export default async function OrderConfirmationPage({ searchParams }: ConfirmationPageProps) {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) {
    redirect('/login?returnUrl=/checkout/confirmation');
  }

  const { orderId } = await searchParams;
  if (!orderId) {
    notFound();
  }

  const order = await getOrderById(orderId);
  if (!order) {
    notFound();
  }
  const products = await getOrderProducts(order);
  const formattedDate = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' }).format(new Date(order.createdAt));
  const shippingName = order.shippingMethodCode.toLowerCase().replace(/\b\w/g, (letter) => letter.toUpperCase());

  return (
    <>
      <main className="flex flex-grow items-center justify-center px-lg py-3xl md:px-xl">
        <div className="flex w-full max-w-[768px] flex-col items-center">
          <div className="mb-xl flex h-24 w-24 items-center justify-center rounded-full bg-status-success/10 text-status-success" aria-label="Order confirmed">
            <span className="confirmation-check" aria-hidden="true" />
          </div>
          <h1 className="mb-sm text-center font-headline-lg text-headline-lg-mobile text-primary md:text-headline-lg">Order placed successfully</h1>
          <p className="mb-xl text-center font-body-md text-body-md text-on-surface-variant">Order #{order.id}</p>

          <section className="mb-xl w-full rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-label="Order summary">
            <div className="grid grid-cols-2 gap-lg md:grid-cols-4">
              <div><p className="mb-xs font-label-caps text-label-caps uppercase text-on-surface-variant">Date</p><p className="font-body-md text-body-md font-medium text-primary">{formattedDate}</p></div>
              <div><p className="mb-xs font-label-caps text-label-caps uppercase text-on-surface-variant">Total</p><p className="font-body-md text-body-md font-medium text-primary">{formatCurrency(order.total)}</p></div>
              <div><p className="mb-xs font-label-caps text-label-caps uppercase text-on-surface-variant">Items</p><p className="font-body-md text-body-md font-medium text-primary">{order.items.length} {order.items.length === 1 ? 'item' : 'items'}</p></div>
              <div><p className="mb-xs font-label-caps text-label-caps uppercase text-on-surface-variant">Shipping</p><p className="font-body-md text-body-md font-medium text-primary">{shippingName}</p></div>
            </div>
          </section>

          <section className="mb-xl w-full" aria-labelledby="items-heading">
            <h2 id="items-heading" className="mb-lg border-b border-border-subtle pb-sm font-product-title text-product-title text-primary">Items in this order</h2>
            <div className="flex flex-col gap-lg">
              {order.items.map((item) => {
                const product = products[item.productId];
                const image = product?.images[0];
                return (
                  <article className="flex items-center gap-xl rounded-lg border border-border-standard bg-surface-card p-md shadow-sm" key={item.productId}>
                    <div className="relative h-20 w-20 flex-shrink-0 overflow-hidden rounded-lg bg-surface-container-low">
                      {image ? <Image className="h-full w-full object-cover" src={image.imageUrl} alt={image.altText || item.productName} width={80} height={80} /> : <span className="flex h-full items-center justify-center px-xs text-center font-label-caps text-label-caps uppercase text-on-surface-variant">Luna</span>}
                    </div>
                    <div className="flex-grow"><h3 className="font-body-md text-body-md font-semibold text-primary">{item.productName}</h3><p className="font-body-md text-body-md text-on-surface-variant">Qty: {item.quantity}</p></div>
                    <p className="text-right font-body-md text-body-md font-medium text-primary">{formatCurrency(item.lineTotal)}</p>
                  </article>
                );
              })}
            </div>
          </section>

          <div className="mt-xl flex w-full flex-col justify-center gap-lg md:flex-row">
            <Link className="flex h-control w-full items-center justify-center rounded-button bg-primary px-xl py-sm font-label-caps text-label-caps uppercase text-on-primary shadow-sm transition-colors hover:bg-primary-container md:w-auto" href={`/orders/${order.id}`}>View order</Link>
            <Link className="flex h-control w-full items-center justify-center rounded-button border border-border-standard bg-surface-card px-xl py-sm font-label-caps text-label-caps uppercase text-primary transition-colors hover:bg-surface-container-low md:w-auto" href="/shop">Continue shopping</Link>
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}
