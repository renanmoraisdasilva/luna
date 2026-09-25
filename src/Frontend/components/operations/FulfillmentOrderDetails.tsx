import Link from 'next/link';
import Icon from '../layout/Icon';
import type { FulfillmentOrder } from '../../lib/api/orders';
import { formatCurrency } from '../../lib/formatters/currency';

const pipeline = [
  { label: 'Confirmed', icon: 'assignment' as const },
  { label: 'Preparing', icon: 'precision' as const },
  { label: 'Shipment Created', icon: 'inventory' as const },
  { label: 'In Transit', icon: 'shopping-bag' as const },
  { label: 'Delivered', icon: 'shopping-bag' as const },
];

function pipelineIndex(status: FulfillmentOrder['orderStatus']) {
  return status === 'Confirmed' ? 0 : 1;
}

function actionLabel(action: FulfillmentOrder['availableAction']) {
  if (action === 'StartPreparing') return 'Start Order Preparation';
  if (action === 'CreateShipment') return 'Create Shipment';
  if (action === 'RetryShipment') return 'Retry Shipment';
  return 'No action available';
}

function statusLabel(status: FulfillmentOrder['orderStatus']) {
  return status === 'ShippingPendingRetry' ? 'Shipping Pending Retry' : status;
}

function formatAddress(order: FulfillmentOrder) {
  const { shippingAddress: address } = order;
  return [
    address.fullName,
    [address.addressLine1, address.addressLine2].filter(Boolean).join(', '),
    `${address.city}, ${address.stateOrProvince} ${address.postalCode}`,
    address.country,
  ];
}

export default function FulfillmentOrderDetails({ order }: { order: FulfillmentOrder }) {
  const activePipelineIndex = pipelineIndex(order.orderStatus);
  const createdAt = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(order.createdAt));
  const addressLines = formatAddress(order);

  return (
    <div className="mx-auto flex w-full max-w-[1280px] flex-col gap-xl px-lg py-3xl md:px-xl">
      <section className="flex flex-col justify-between gap-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm md:flex-row md:items-end">
        <div>
          <div className="mb-sm flex items-center gap-xs font-label-caps text-label-caps text-secondary">
            <Link className="hover:text-primary" href="/operations/fulfillment">Fulfillment</Link>
            <Icon name="chevron-right" className="h-3.5 w-3.5" />
            <span className="font-semibold text-on-surface">Order #{order.orderId}</span>
          </div>
          <div className="flex flex-wrap items-center gap-md">
            <h1 className="font-headline-lg text-headline-lg-mobile text-on-surface md:text-headline-lg">Order #{order.orderId}</h1>
            <span className="rounded-full bg-primary/10 px-md py-xs font-label-caps text-label-caps uppercase text-primary">{statusLabel(order.orderStatus)}</span>
          </div>
          <p className="mt-xs font-status-pill text-status-pill text-secondary">Created {createdAt}</p>
        </div>
        <Link className="inline-flex h-control items-center gap-xs rounded-button border border-border-standard bg-surface-card px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface shadow-sm hover:bg-surface-container-low" href="/operations/fulfillment">
          <Icon name="chevron-left" className="h-[18px] w-[18px]" />Back to queue
        </Link>
      </section>

      <section className="space-y-xl rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="fulfillment-pipeline-heading">
        <div className="flex flex-col justify-between gap-md sm:flex-row sm:items-center">
          <div>
            <span id="fulfillment-pipeline-heading" className="font-label-caps text-label-caps uppercase text-secondary">Fulfillment Pipeline</span>
            <p className="mt-xs flex items-center gap-xs font-body-md text-body-md text-on-surface-variant"><Icon name="info" className="h-5 w-5" />Current step: {statusLabel(order.orderStatus)}.</p>
          </div>
          <span className="rounded-full bg-surface-container-low px-md py-xs font-status-pill text-status-pill text-secondary">Step {activePipelineIndex + 1} of {pipeline.length}</span>
        </div>
        <div className="grid grid-cols-2 gap-lg sm:grid-cols-5">
          {pipeline.map((step, index) => {
            const isCurrent = index === activePipelineIndex;
            const isComplete = index < activePipelineIndex;
            return <div className={`flex flex-col items-center text-center ${index > activePipelineIndex ? 'opacity-40' : ''}`} key={step.label}><div className={`flex h-8 w-8 items-center justify-center rounded-full ${isComplete || isCurrent ? 'bg-primary text-on-primary' : 'bg-surface-container-high text-outline'} ${isCurrent ? 'ring-4 ring-primary/10' : ''}`}><Icon name={isComplete ? 'assignment' : step.icon} className="h-[18px] w-[18px]" /></div><span className={`mt-sm font-label-caps text-label-caps ${isCurrent ? 'font-semibold text-primary' : 'text-secondary'}`}>{index + 1}. {step.label}</span><span className="mt-xs text-[11px] text-secondary">{isCurrent ? 'Current' : isComplete ? 'Complete' : 'Pending'}</span></div>;
          })}
        </div>
      </section>

      <div className="grid grid-cols-1 gap-xl lg:grid-cols-12">
        <div className="space-y-xl lg:col-span-8">
          <section className="space-y-xl rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="order-items-heading">
            <div className="flex items-center justify-between border-b border-border-subtle pb-sm"><div className="flex items-center gap-sm"><Icon name="inventory" className="h-[22px] w-[22px] text-primary" /><h2 id="order-items-heading" className="font-product-title text-product-title text-primary">Order Items</h2></div><span className="font-label-caps text-label-caps text-secondary">{order.items.length} line items</span></div>
            <div className="overflow-x-auto"><table className="w-full min-w-[560px] text-left"><thead><tr className="border-b border-border-subtle font-label-caps text-label-caps uppercase text-secondary"><th className="pb-md">Product</th><th className="pb-md">SKU</th><th className="pb-md text-center">Qty</th><th className="pb-md text-right">Unit Price</th><th className="pb-md text-right">Total</th></tr></thead><tbody className="divide-y divide-border-subtle">{order.items.map((item) => <tr key={item.productId}><td className="py-md pr-md"><span className="font-product-title text-[15px] font-semibold text-primary">{item.productName}</span></td><td className="py-md pr-md font-label-caps text-label-caps text-secondary">{item.sku}</td><td className="py-md pr-md text-center"><span className="inline-flex h-7 w-7 items-center justify-center rounded-md bg-surface-container-low font-label-caps text-label-caps font-semibold text-primary">{item.quantity}</span></td><td className="py-md pr-md text-right font-body-md text-secondary">{formatCurrency(item.unitPrice)}</td><td className="py-md text-right font-product-title text-[15px] font-semibold text-primary">{formatCurrency(item.lineTotal)}</td></tr>)}</tbody></table></div>
            <div className="ml-auto w-full max-w-xs space-y-sm rounded-lg bg-surface-container-low p-lg font-body-md text-body-md"><div className="flex justify-between text-on-surface-variant"><span>Subtotal</span><span className="text-on-surface">{formatCurrency(order.subtotal)}</span></div><div className="flex justify-between text-on-surface-variant"><span>Shipping</span><span className="text-on-surface">{formatCurrency(order.shippingCost)}</span></div><div className="flex justify-between border-t border-border-standard pt-sm font-product-title text-product-title text-primary"><span>Total Amount</span><span>{formatCurrency(order.total)}</span></div></div>
          </section>
          <section className="space-y-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="shipping-address-heading"><div className="flex items-center gap-sm"><Icon name="location" className="h-[22px] w-[22px] text-primary" /><h2 id="shipping-address-heading" className="font-product-title text-product-title text-on-surface">Destination Shipping Address</h2></div><address className="not-italic rounded-lg bg-surface-base p-lg font-body-md text-body-md text-on-surface-variant">{addressLines.map((line) => <span className="block" key={line}>{line}</span>)}<span className="mt-md block border-t border-border-standard pt-md font-semibold text-primary">Shipping method: {order.shippingMethodCode}</span></address></section>
        </div>

        <aside className="space-y-xl lg:col-span-4">
          <section className="space-y-lg rounded-lg bg-primary p-xl text-on-primary shadow-md" aria-labelledby="fulfillment-action-heading"><div className="flex items-center justify-between"><span className="font-label-caps text-label-caps uppercase tracking-wider text-on-primary-container">Fulfillment Action</span><span className="rounded-full bg-primary-container px-sm py-xs font-label-caps text-label-caps text-on-primary">Step {activePipelineIndex + 1}</span></div><div><h2 id="fulfillment-action-heading" className="font-headline-lg text-headline-lg-mobile">{actionLabel(order.availableAction)}</h2><p className="mt-xs font-body-md text-body-md text-on-primary-container">Actions are processed by the Orders workflow.</p></div><button className="flex h-control w-full cursor-not-allowed items-center justify-center gap-sm rounded-button bg-surface-card px-xl py-sm font-label-caps text-label-caps uppercase text-primary opacity-60" type="button" disabled><Icon name="precision" className="h-5 w-5" />{actionLabel(order.availableAction)}</button><p className="border-t border-primary-container pt-md text-center font-status-pill text-status-pill text-on-primary-container">Workflow commands are not available in the current Orders API.</p></section>
          <section className="space-y-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="order-metadata-heading"><div className="flex items-center justify-between border-b border-border-standard pb-sm"><h2 id="order-metadata-heading" className="font-product-title text-product-title text-on-surface">Metadata &amp; Routing</h2><Icon name="info" className="h-5 w-5 text-secondary" /></div><div className="space-y-md"><div className="flex items-center justify-between rounded-button bg-surface-container-low p-sm"><div className="flex flex-col"><span className="font-label-caps text-label-caps uppercase text-secondary">Payment Status</span><span className="font-status-pill text-status-pill text-secondary">{order.paymentStatus}</span></div><span className="rounded-full bg-status-success/10 px-sm py-xs font-label-caps text-label-caps font-semibold text-status-success">{order.paymentStatus}</span></div><div className="flex items-center justify-between"><span className="font-label-caps text-label-caps uppercase text-secondary">Customer</span><span className="font-status-pill text-status-pill font-semibold text-on-surface">{order.customerName}</span></div><div className="flex items-center justify-between"><span className="font-label-caps text-label-caps uppercase text-secondary">Order ID</span><span className="max-w-[180px] truncate font-label-caps text-label-caps font-semibold text-primary">#{order.orderId}</span></div></div></section>
        </aside>
      </div>
    </div>
  );
}