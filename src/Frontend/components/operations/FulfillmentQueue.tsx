'use client';

import { useQueries, useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';
import Icon from '../layout/Icon';
import { getFulfillmentQueue } from '../../lib/api/orders';
import type { FulfillmentOrderSummary, FulfillmentStatus } from '../../lib/api/orders';
import { formatCurrency } from '../../lib/formatters/currency';

type QueueFilter = 'All' | FulfillmentStatus;

const filters: QueueFilter[] = ['All', 'Confirmed', 'Preparing', 'ShippingPendingRetry'];

function statusLabel(status: FulfillmentStatus) {
  return status === 'ShippingPendingRetry' ? 'Shipping Pending Retry' : status;
}

function statusClass(status: FulfillmentStatus) {
  if (status === 'ShippingPendingRetry') return 'bg-error-container text-status-error';
  if (status === 'Preparing') return 'bg-status-warning/15 text-status-warning';
  return 'bg-primary/10 text-primary';
}

function actionLabel(action: FulfillmentOrderSummary['availableAction']) {
  if (action === 'StartPreparing') return 'Start Preparing';
  if (action === 'CreateShipment') return 'Create Shipment';
  if (action === 'RetryShipment') return 'Retry Shipment';
  return 'Unavailable';
}

function actionIcon(action: FulfillmentOrderSummary['availableAction']) {
  if (action === 'StartPreparing') return 'play' as const;
  if (action === 'CreateShipment') return 'inventory' as const;
  return 'replay' as const;
}

function formatCreatedAt(value: string) {
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export default function FulfillmentQueue() {
  const [activeFilter, setActiveFilter] = useState<QueueFilter>('All');
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  const search = searchTerm.trim();

  const queueQuery = useQuery({
    queryKey: ['fulfillment-queue', activeFilter, search, page, pageSize],
    queryFn: () => getFulfillmentQueue({
      status: activeFilter === 'All' ? undefined : activeFilter,
      search,
      page,
      pageSize,
    }),
  });
  const countQueries = useQueries({
    queries: filters.map((filter) => ({
      queryKey: ['fulfillment-count', filter],
      queryFn: () => getFulfillmentQueue({
        status: filter === 'All' ? undefined : filter,
        page: 1,
        pageSize: 1,
      }),
      staleTime: 30_000,
    })),
  });

  const orders = queueQuery.data?.items ?? [];
  const totalCount = queueQuery.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  function countFor(filter: QueueFilter) {
    return countQueries[filters.indexOf(filter)]?.data?.totalCount ?? 0;
  }

  function changeFilter(filter: QueueFilter) {
    setActiveFilter(filter);
    setPage(1);
  }

  function changePageSize(value: number) {
    setPageSize(value);
    setPage(1);
  }

  return (
    <main className="mx-auto flex w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
      <div className="flex flex-col justify-between gap-lg md:flex-row md:items-end">
        <div className="flex flex-col gap-xs">
          <div className="flex items-center gap-sm font-label-caps text-label-caps uppercase tracking-wider text-secondary">
            <span>Fulfillment</span><span className="text-secondary/40">/</span><span className="font-semibold text-primary">Orders</span>
          </div>
          <div className="flex items-center gap-md">
            <h1 className="font-headline-lg text-headline-lg-mobile tracking-tight text-on-surface md:text-headline-lg">Fulfillment</h1>
            <span className="rounded-full bg-surface-container-low px-sm py-xs font-label-caps text-label-caps uppercase text-secondary">Preview</span>
          </div>
          <p className="font-body-md text-body-md text-secondary">Manage confirmed orders and prepare them for shipment.</p>
        </div>
        <div className="flex w-full items-center gap-sm md:w-auto">
          <div className="relative w-full md:w-80">
            <Icon name="search" className="absolute left-md top-1/2 h-[18px] w-[18px] -translate-y-1/2 text-outline" />
            <label className="sr-only" htmlFor="fulfillment-search">Search fulfillment orders</label>
            <input id="fulfillment-search" className="h-control w-full rounded-button border border-border-standard bg-surface-card py-xs pl-3xl pr-md font-body-md text-body-md text-on-surface shadow-sm placeholder:text-outline focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary" placeholder="Search orders by ID, customer name, or SKU..." type="search" value={searchTerm} onChange={(event) => { setSearchTerm(event.target.value); setPage(1); }} />
          </div>
          <button className="inline-flex h-control shrink-0 items-center gap-xs rounded-button border border-border-standard bg-surface-card px-md font-label-caps text-label-caps uppercase tracking-wider text-on-surface shadow-sm transition-colors hover:bg-surface-container-low" type="button">
            <Icon name="description" className="h-[18px] w-[18px]" />
            Filter
          </button>
        </div>
      </div>

      <section className="grid grid-cols-1 gap-xl sm:grid-cols-2 lg:grid-cols-4" aria-label="Fulfillment summary">
        <Metric label="Total Orders" value={countFor('All')} detail="Total active orders" icon="inventory" />
        <Metric label="Confirmed" value={countFor('Confirmed')} detail="Awaiting preparation" icon="assignment" />
        <Metric label="Preparing" value={countFor('Preparing')} detail="In preparation" icon="precision" tone="warning" />
        <Metric label="Pending Retry" value={countFor('ShippingPendingRetry')} detail="Requires retry" icon="sync-problem" tone="error" />
      </section>

      <section className="overflow-hidden rounded-lg border border-border-standard bg-surface-card shadow-sm" aria-labelledby="fulfillment-orders-heading">
        <div className="flex flex-col justify-between gap-md border-b border-border-standard p-lg sm:flex-row sm:items-center">
          <h2 id="fulfillment-orders-heading" className="sr-only">Fulfillment orders</h2>
          <div className="flex max-w-full gap-xs overflow-x-auto rounded-button bg-surface-container-low p-xs">
            {filters.map((filter) => (
              <button key={filter} className={`flex shrink-0 items-center gap-xs rounded-button px-md py-sm font-label-caps text-label-caps transition-colors ${activeFilter === filter ? 'bg-primary text-on-primary shadow-sm' : 'text-secondary hover:bg-surface-container hover:text-on-surface'}`} type="button" onClick={() => changeFilter(filter)} aria-pressed={activeFilter === filter}>
                {filter === 'ShippingPendingRetry' ? <span className="h-1.5 w-1.5 rounded-full bg-status-warning" aria-hidden="true" /> : null}
                <span>{filter === 'All' ? 'All' : statusLabel(filter)}</span>
                <span className={`rounded-lg px-xs py-[1px] text-[10px] ${activeFilter === filter ? 'bg-primary-container text-on-primary' : 'bg-surface-container text-secondary'}`}>{countFor(filter)}</span>
              </button>
            ))}
          </div>
          <div className="flex items-center gap-sm self-end sm:self-auto">
            <button className="rounded-lg p-xs text-secondary transition-colors hover:bg-surface-container-low hover:text-on-surface" type="button" title="Refresh fulfillment queue" onClick={() => queueQuery.refetch()}><Icon name="refresh" className="h-5 w-5" /></button>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[960px] table-fixed border-collapse text-left lg:min-w-0">
            <colgroup>
              <col className="w-[15%]" />
              <col className="w-[16%]" />
              <col className="w-[9%]" />
              <col className="w-[9%]" />
              <col className="w-[13%]" />
              <col className="w-[14%]" />
              <col className="w-[12%]" />
              <col className="w-[12%]" />
            </colgroup>
            <thead>
              <tr className="bg-surface-container-low font-label-caps text-label-caps uppercase tracking-wider text-secondary">
                <th className="px-xl py-md font-semibold">Order</th>
                <th className="px-xl py-md font-semibold">Customer</th>
                <th className="px-xl py-md font-semibold">Items</th>
                <th className="px-xl py-md font-semibold">Total</th>
                <th className="px-xl py-md font-semibold">Payment</th>
                <th className="px-xl py-md font-semibold">Status</th>
                <th className="px-xl py-md font-semibold">Created</th>
                <th className="px-xl py-md text-right font-semibold">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border-subtle text-on-surface">
              {queueQuery.isLoading ? <tr><td className="px-xl py-2xl text-center font-body-md text-body-md text-secondary" colSpan={8}>Loading fulfillment orders...</td></tr> : null}
              {queueQuery.isError ? <tr><td className="px-xl py-2xl text-center font-body-md text-body-md text-status-error" colSpan={8}>Unable to load fulfillment orders. Refresh and try again.</td></tr> : null}
              {!queueQuery.isLoading && !queueQuery.isError && orders.map((order) => (
                <tr className="transition-colors hover:bg-surface-container-low/50" key={order.orderId}>
                  <td className="overflow-hidden px-xl py-lg align-top"><div className="flex min-w-0 items-center gap-xs"><Link className="min-w-0 truncate font-product-title text-[15px] font-semibold text-primary hover:underline" href={`/operations/fulfillment/${order.orderId}`} title={`Open order ${order.orderId}`}>#{order.orderId}</Link><button className="shrink-0 text-outline transition-colors hover:text-primary" type="button" title={`Copy order ${order.orderId}`} onClick={() => navigator.clipboard?.writeText(order.orderId)}><Icon name="copy" className="h-4 w-4" /></button></div></td>
                  <td className="overflow-hidden px-xl py-lg align-top"><span className="block truncate font-product-title text-[14px] font-semibold">{order.customerName}</span></td>
                  <td className="px-xl py-lg align-top"><span className="block font-status-pill text-status-pill font-semibold">{order.itemCount} {order.itemCount === 1 ? 'item' : 'items'}</span></td>
                  <td className="px-xl py-lg align-top font-product-title text-[14px] font-semibold">{formatCurrency(order.total)}</td>
                  <td className="overflow-hidden px-xl py-lg align-top"><span className={`inline-flex max-w-full items-center gap-xs rounded-full px-sm py-xs font-label-caps text-label-caps ${order.paymentStatus === 'Authorized' ? 'bg-status-success/10 text-status-success' : 'bg-surface-container text-secondary'}`}><span className={`h-1.5 w-1.5 shrink-0 rounded-full ${order.paymentStatus === 'Authorized' ? 'bg-status-success' : 'bg-secondary'}`} aria-hidden="true" /><span className="truncate">{order.paymentStatus}</span></span></td>
                  <td className="overflow-hidden px-xl py-lg align-top"><span className={`inline-flex max-w-full rounded-full px-sm py-xs font-label-caps text-label-caps font-semibold ${statusClass(order.orderStatus)}`}><span className="truncate">{statusLabel(order.orderStatus)}</span></span></td>
                  <td className="whitespace-normal px-xl py-lg align-top font-label-caps text-[12px] text-on-surface">{formatCreatedAt(order.createdAt)}</td>
                  <td className="px-xl py-lg text-right align-top"><button className={`inline-flex max-w-full items-center justify-end gap-xs rounded-button px-md py-sm text-right font-label-caps text-label-caps uppercase tracking-wider shadow-sm disabled:cursor-not-allowed ${order.orderStatus === 'ShippingPendingRetry' ? 'bg-status-error text-on-primary' : order.orderStatus === 'Preparing' ? 'bg-surface-container-high text-on-surface' : 'bg-primary text-on-primary'}`} type="button" disabled title="Fulfillment commands are not available in the Orders API yet"><Icon name={actionIcon(order.availableAction)} className="h-4 w-4 shrink-0" /><span>{actionLabel(order.availableAction)}</span></button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {!queueQuery.isLoading && !queueQuery.isError && orders.length === 0 ? <p className="p-2xl text-center font-body-md text-body-md text-secondary">No fulfillment orders match the current filters.</p> : null}
        <div className="flex flex-col justify-between gap-md border-t border-border-standard bg-surface-container-low px-xl py-md font-label-caps text-label-caps text-secondary sm:flex-row sm:items-center">
          <div className="flex items-center gap-md"><span>Showing <strong className="text-on-surface">{orders.length ? `${(page - 1) * pageSize + 1}-${(page - 1) * pageSize + orders.length}` : '0'}</strong> of <strong className="text-on-surface">{totalCount}</strong> orders</span><label className="flex items-center gap-xs" htmlFor="fulfillment-rows">Rows:<select id="fulfillment-rows" className="rounded-lg bg-surface-card px-sm py-[2px] text-on-surface shadow-sm focus:outline-none" value={pageSize} onChange={(event) => changePageSize(Number(event.target.value))}><option value={5}>5</option><option value={10}>10</option><option value={25}>25</option><option value={50}>50</option></select></label></div>
          <div className="flex items-center gap-xs" aria-label="Pagination"><button className="rounded-lg p-xs text-secondary disabled:text-outline/40" type="button" disabled={page === 1 || queueQuery.isLoading} onClick={() => setPage((current) => current - 1)} title="Previous page"><Icon name="chevron-left" className="h-[18px] w-[18px]" /></button>{Array.from({ length: pageCount }, (_, index) => index + 1).map((pageNumber) => <button key={pageNumber} className={`flex h-7 w-7 items-center justify-center rounded-lg font-label-caps text-label-caps font-semibold ${pageNumber === page ? 'bg-primary text-on-primary' : 'text-secondary transition-colors hover:bg-surface-card hover:text-on-surface'}`} type="button" aria-current={pageNumber === page ? 'page' : undefined} onClick={() => setPage(pageNumber)}>{pageNumber}</button>)}<button className="rounded-lg p-xs text-secondary disabled:text-outline/40" type="button" disabled={page >= pageCount || queueQuery.isLoading} onClick={() => setPage((current) => current + 1)} title="Next page"><Icon name="chevron-right" className="h-[18px] w-[18px]" /></button></div>
        </div>
      </section>
    </main>
  );
}

function Metric({ label, value, detail, icon, tone = 'default' }: { label: string; value: number; detail: string; icon: 'inventory' | 'assignment' | 'precision' | 'sync-problem'; tone?: 'default' | 'warning' | 'error' }) {
  const toneClass = tone === 'error' ? 'text-status-error' : tone === 'warning' ? 'text-status-warning' : 'text-primary';
  return <article className="flex min-h-[147px] flex-col justify-between rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm"><div className="flex items-start justify-between"><div className="flex flex-col gap-xs"><span className="font-label-caps text-label-caps uppercase text-secondary">{label}</span><span className={`font-headline-lg text-headline-lg-mobile ${tone === 'error' ? toneClass : 'text-on-surface'}`}>{value}</span></div><span className={`rounded-button bg-surface-container-low p-sm ${toneClass}`}><Icon name={icon} className="h-[22px] w-[22px]" /></span></div><span className="mt-lg border-t border-border-standard pt-md font-label-caps text-label-caps text-secondary">{detail}</span></article>;
}
