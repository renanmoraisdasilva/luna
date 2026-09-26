import Image from 'next/image';
import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import Footer from '../../../../components/layout/Footer';
import PrintButton from '../../../../components/orders/PrintButton';
import { getCurrentUserServer } from '../../../../lib/auth-server';
import { getOrderById, getOrderProducts, getShipmentTracking } from '../../../../lib/server/orders';
import { formatCurrency } from '../../../../lib/formatters/currency';
import type { Order } from '../../../../lib/api/orders';
import type { ShipmentTracking } from '../../../../lib/api/shipping';

type OrderDetailsProps = { params: Promise<{ orderId: string }> };

const timeline = [
  { key: 'Confirmed', label: 'Order placed' },
  { key: 'Preparing', label: 'Preparing' },
  { key: 'Shipped', label: 'Shipped' },
  { key: 'Delivered', label: 'Delivered' },
];

function statusLabel(status: string) {
  return status === 'PaymentFailed' ? 'Payment failed' : status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function shipmentStatusLabel(status: string) {
  if (status === 'InTransit') return 'On the way';
  if (status === 'Created') return 'Shipment created';
  return statusLabel(status);
}

function timelineIndex(status: string) {
  if (status === 'Delivered') return 3;
  if (status === 'Shipped') return 2;
  if (status === 'Preparing') return 1;
  return 0;
}

function formatAddress(order: Order) {
  const { shippingAddress: address } = order;
  return [address.fullName, [address.addressLine1, address.addressLine2].filter(Boolean).join(', '), `${address.city}, ${address.stateOrProvince} ${address.postalCode}`, address.country];
}

function ShipmentTimeline({ order, tracking }: { order: Order; tracking: ShipmentTracking | null }) {
  const activeIndex = timelineIndex(order.status);
  return <div className="space-y-xl">
    <div className="relative grid grid-cols-4 items-start">
      <div className="absolute left-[12.5%] right-[12.5%] top-2.5 border-t-2 border-dashed border-border-standard" aria-hidden="true" />
      <div className="absolute left-[12.5%] top-2.5 h-0.5 bg-primary transition-all" style={{ width: `${Math.max(0, activeIndex / 3 * 75)}%` }} aria-hidden="true" />
      {timeline.map((step, index) => {
        const complete = index < activeIndex;
        const active = index === activeIndex;
        return <div className="relative z-10 flex flex-col items-center" key={step.key}>
          <div className={`flex h-xl w-xl items-center justify-center rounded-full border-4 border-white shadow-sm ${complete || active ? 'bg-primary' : 'bg-surface-card ring-1 ring-border-standard'}`}>
            {complete ? <span className="timeline-check" aria-hidden="true" /> : active ? <span className="h-2 w-2 rounded-full bg-primary" aria-hidden="true" /> : null}
          </div>
          <span className={`mt-sm bg-surface-card px-sm text-center font-status-pill text-status-pill ${active ? 'font-bold text-primary' : 'text-on-surface-variant'}`}>{step.label}</span>
        </div>;
      })}
    </div>
    {tracking ? <div className="flex flex-col gap-md rounded-lg bg-surface-base p-lg sm:flex-row sm:items-center sm:justify-between">
      <div><p className="font-label-caps text-label-caps uppercase text-on-surface-variant">Tracking number</p><p className="mt-xs font-product-title text-product-title font-semibold text-primary">{tracking.trackingNumber}</p></div>
      <div className="text-left sm:text-right"><p className="font-label-caps text-label-caps uppercase text-on-surface-variant">Shipment status</p><p className="mt-xs font-body-md text-body-md font-semibold text-primary">{shipmentStatusLabel(tracking.status)}</p></div>
    </div> : <p className="rounded-lg bg-surface-base p-lg font-body-md text-body-md text-on-surface-variant">Shipment tracking will appear here once your order has been dispatched.</p>}
  </div>;
}

function DetailCard({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm"><h2 className="mb-lg border-b border-border-standard pb-sm font-product-title text-product-title text-on-surface">{title}</h2>{children}</section>;
}

export default async function OrderDetailsPage({ params }: OrderDetailsProps) {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) redirect('/login?returnUrl=/orders');
  const { orderId } = await params;
  const order = await getOrderById(orderId);
  if (!order) notFound();
  const [products, tracking] = await Promise.all([
    getOrderProducts(order),
    order.shipmentId ? getShipmentTracking(order.shipmentId) : Promise.resolve(null),
  ]);
  const createdDate = new Date(order.createdAt);
  const addressLines = formatAddress(order);
  const activityDate = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(createdDate);

  return (
    <>
      <main className="mx-auto flex w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
        <div className="flex flex-col items-start justify-between gap-lg md:flex-row md:items-center">
          <div><Link className="mb-sm inline-block font-label-caps text-label-caps uppercase text-secondary hover:text-primary" href="/orders">Back to orders</Link><h1 className="font-headline-lg text-headline-lg-mobile text-on-surface md:text-headline-lg">Order #{order.id}</h1></div>
          <PrintButton />
        </div>

        <div className="grid grid-cols-1 gap-xl md:grid-cols-3">
          <div className="space-y-lg md:col-span-2">
            <DetailCard title="Track shipment"><ShipmentTimeline order={order} tracking={tracking} /></DetailCard>
            <DetailCard title="Activity log"><ul className="space-y-lg">{[{ label: order.status === 'PaymentFailed' ? 'Payment failed' : 'Payment processed', date: activityDate }, { label: 'Order received', date: activityDate }].map((activity) => <li className="flex items-start gap-md" key={activity.label}><span className="mt-xs h-3 w-3 rounded-full border-2 border-outline" aria-hidden="true" /><div><p className="font-body-md text-body-md text-on-surface">{activity.label}</p><p className="mt-xs font-label-caps text-label-caps text-on-surface-variant">{activity.date}</p></div></li>)}</ul></DetailCard>
            <DetailCard title="Items in your order"><div className="space-y-lg">{order.items.map((item) => { const image = products[item.productId]?.images[0]; return <article className="flex gap-lg rounded-lg border border-border-standard bg-surface-base p-lg" key={item.productId}><div className="relative h-24 w-24 flex-shrink-0 overflow-hidden rounded-lg bg-surface-container-low">{image ? <Image className="h-full w-full object-cover" src={image.imageUrl} alt={image.altText || item.productName} width={96} height={96} /> : <span className="flex h-full items-center justify-center font-label-caps text-label-caps uppercase text-on-surface-variant">Luna</span>}</div><div className="flex min-w-0 flex-grow justify-between gap-lg"><div><h3 className="font-body-md text-body-md font-bold text-on-surface">{item.productName}</h3><p className="mt-xs font-label-caps text-label-caps text-on-surface-variant">Qty: {item.quantity}</p></div><p className="whitespace-nowrap text-right font-body-md text-body-md font-medium text-on-surface">{formatCurrency(item.lineTotal)}</p></div></article>; })}</div></DetailCard>
          </div>

          <div className="space-y-lg">
            <DetailCard title="Order summary"><div className="mb-lg space-y-sm border-b border-border-standard pb-lg font-body-md text-body-md"><div className="flex justify-between text-on-surface-variant"><span>Subtotal</span><span>{formatCurrency(order.subtotal)}</span></div><div className="flex justify-between text-on-surface-variant"><span>Shipping</span><span>{formatCurrency(order.shippingCost)}</span></div></div><div className="flex items-center justify-between font-body-md text-body-md font-medium text-on-surface"><span>Total</span><span className="font-semibold">{formatCurrency(order.total)}</span></div></DetailCard>
            <DetailCard title="Shipping address"><address className="not-italic font-body-md text-body-md text-on-surface-variant">{addressLines.map((line) => <span className="block" key={line}>{line}</span>)}</address></DetailCard>
            <DetailCard title="Payment method"><div className="flex items-center gap-md font-body-md text-body-md text-on-surface-variant"><span className="payment-card-icon" aria-hidden="true" /><span>{order.status === 'PaymentFailed' ? 'Payment failed' : 'Payment processed'}</span></div></DetailCard>
            <div className="rounded-lg border border-border-standard bg-surface-card p-xl"><p className="font-label-caps text-label-caps uppercase text-on-surface-variant">Status</p><p className="mt-xs font-body-md text-body-md font-semibold text-primary">{statusLabel(order.status)}</p></div>
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}
