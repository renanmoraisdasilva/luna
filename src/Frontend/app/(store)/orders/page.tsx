import Link from 'next/link';
import { redirect } from 'next/navigation';
import Footer from '../../../components/layout/Footer';
import { getCurrentUserServer } from '../../../lib/auth-server';
import { getOrderSummaries } from '../../../lib/server/orders';
import { formatCurrency } from '../../../lib/formatters/currency';
import type { OrderSummary } from '../../../lib/api/orders';

function statusLabel(status: string) {
  return status === 'PaymentFailed' ? 'Payment failed' : status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function statusClass(status: string) {
  if (status === 'Preparing' || status === 'Confirmed') return 'bg-primary-fixed text-on-primary-fixed';
  if (status === 'PaymentFailed' || status === 'Cancelled') return 'bg-error-container text-on-error-container';
  return 'border border-border-standard bg-surface-container-low text-on-surface-variant';
}

function OrderCard({ order }: { order: OrderSummary }) {
  const date = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' }).format(new Date(order.createdAt));
  return (
    <article className="flex flex-col items-start justify-between gap-lg rounded-lg border border-border-standard bg-surface-card p-lg transition-all hover:border-outline hover:shadow-sm md:flex-row md:items-center">
      <div className="flex min-w-0 flex-grow flex-col gap-sm">
        <div className="flex flex-wrap items-center gap-md">
          <h2 className="font-product-title text-product-title text-on-surface">Order #{order.id}</h2>
          <span className={`inline-flex items-center gap-xs rounded-full px-md py-xs font-status-pill text-status-pill ${statusClass(order.status)}`}>
            <span className="h-2 w-2 rounded-full bg-current" aria-hidden="true" />
            {statusLabel(order.status)}
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-x-lg gap-y-xs font-body-md text-body-md text-on-surface-variant">
          <span>{date}</span><span className="h-1.5 w-1.5 rounded-full bg-border-standard" aria-hidden="true" />
          <span>{order.itemCount} {order.itemCount === 1 ? 'item' : 'items'}</span><span className="h-1.5 w-1.5 rounded-full bg-border-standard" aria-hidden="true" />
          <span className="font-bold text-on-surface">{formatCurrency(order.total)}</span>
        </div>
      </div>
      <Link className="flex h-control w-full items-center justify-center rounded-button border border-border-standard bg-surface-card px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface shadow-sm transition-colors hover:bg-surface-container-low md:w-auto" href={`/orders/${order.id}`}>View order</Link>
    </article>
  );
}

export default async function OrdersPage() {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) redirect('/login?returnUrl=/orders');
  const orders = await getOrderSummaries();

  return (
    <>
      <main className="mx-auto flex min-h-[calc(100vh-64px)] w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
        <h1 className="font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">My orders</h1>
        {orders.length > 0 ? <div className="flex flex-col gap-xl">{orders.map((order) => <OrderCard key={order.id} order={order} />)}</div> : <section className="rounded-lg border border-border-standard bg-surface-card p-2xl text-center"><h2 className="font-product-title text-product-title text-primary">No orders yet</h2><p className="mt-sm font-body-md text-body-md text-on-surface-variant">Your completed purchases will appear here.</p><Link className="mt-xl inline-flex h-control items-center rounded-button bg-primary px-xl py-sm font-label-caps text-label-caps uppercase text-on-primary" href="/shop">Start shopping</Link></section>}
      </main>
      <Footer />
    </>
  );
}
