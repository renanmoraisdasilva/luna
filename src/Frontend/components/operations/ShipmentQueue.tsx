'use client';

import axios from 'axios';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';
import Icon from '../layout/Icon';
import { markShipmentDelivered, markShipmentInTransit } from '../../lib/api/orders';
import { getShipments, type ShipmentStatus } from '../../lib/api/shipping';

type ShipmentFilter = 'All' | ShipmentStatus;

const filters: ShipmentFilter[] = ['All', 'Created', 'InTransit', 'Delivered'];

function statusLabel(status: ShipmentStatus) {
  return status === 'InTransit' ? 'In Transit' : status;
}

function statusClass(status: ShipmentStatus) {
  if (status === 'Delivered') return 'bg-status-success/10 text-status-success';
  if (status === 'InTransit') return 'bg-tertiary-fixed text-tertiary';
  return 'bg-secondary-container/60 text-primary';
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function commandErrorMessage(error: unknown) {
  if (axios.isAxiosError<{ message?: string }>(error)) {
    return error.response?.data?.message ?? 'The shipment command could not be completed.';
  }

  return 'The shipment command could not be completed.';
}

export default function ShipmentQueue() {
  const queryClient = useQueryClient();
  const [activeFilter, setActiveFilter] = useState<ShipmentFilter>('All');
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  const search = searchTerm.trim();

  const inTransitMutation = useMutation({
    mutationFn: markShipmentInTransit,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipments'] }),
  });
  const deliveredMutation = useMutation({
    mutationFn: markShipmentDelivered,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipments'] }),
  });
  const shipmentsQuery = useQuery({
    queryKey: ['shipments', activeFilter, search, page, pageSize],
    queryFn: () => getShipments({ status: activeFilter === 'All' ? undefined : activeFilter, search, page, pageSize }),
  });
  const countQueries = useQueries({
    queries: filters.map((filter) => ({
      queryKey: ['shipment-count', filter],
      queryFn: () => getShipments({ status: filter === 'All' ? undefined : filter, page: 1, pageSize: 1 }),
      staleTime: 30_000,
    })),
  });

  const shipments = shipmentsQuery.data?.items ?? [];
  const totalCount = shipmentsQuery.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  function countFor(filter: ShipmentFilter) {
    return countQueries[filters.indexOf(filter)]?.data?.totalCount ?? 0;
  }

  function changeFilter(filter: ShipmentFilter) {
    setActiveFilter(filter);
    setPage(1);
  }

  function runAction(shipmentId: string, action: 'MarkInTransit' | 'MarkDelivered' | 'None') {
    if (action === 'MarkInTransit') inTransitMutation.mutate(shipmentId);
    if (action === 'MarkDelivered') deliveredMutation.mutate(shipmentId);
  }

  const commandError = inTransitMutation.error ?? deliveredMutation.error;
  const commandPending = inTransitMutation.isPending || deliveredMutation.isPending;

  return (
    <main className="mx-auto flex w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
      <div className="flex flex-col justify-between gap-lg md:flex-row md:items-end">
        <div className="flex flex-col gap-xs">
          <div className="flex items-center gap-sm font-label-caps text-label-caps uppercase tracking-wider text-secondary"><span>Logistics</span><span className="text-secondary/40">/</span><span className="font-semibold text-primary">Shipments</span></div>
          <h1 className="font-headline-lg text-headline-lg-mobile tracking-tight text-on-surface md:text-headline-lg">Shipments</h1>
          <p className="font-body-md text-body-md text-secondary">Track and update outgoing shipments across the delivery lifecycle.</p>
        </div>
        <div className="relative w-full md:w-80">
          <Icon name="search" className="absolute left-md top-1/2 h-[18px] w-[18px] -translate-y-1/2 text-outline" />
          <label className="sr-only" htmlFor="shipment-search">Search shipments</label>
          <input id="shipment-search" className="h-control w-full rounded-button border border-border-standard bg-surface-card py-xs pl-3xl pr-md font-body-md text-body-md text-on-surface shadow-sm placeholder:text-outline focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary" placeholder="Search tracking, order, or customer..." type="search" value={searchTerm} onChange={(event) => { setSearchTerm(event.target.value); setPage(1); }} />
        </div>
      </div>

      <section className="grid grid-cols-1 gap-md sm:grid-cols-2 lg:grid-cols-4" aria-label="Shipment summary">
        <Metric label="Total Active" value={countFor('All')} icon="inventory" />
        <Metric label="Created" value={countFor('Created')} icon="assignment" />
        <Metric label="In Transit" value={countFor('InTransit')} icon="shopping-bag" />
        <Metric label="Delivered" value={countFor('Delivered')} icon="precision" tone="success" />
      </section>

      <section className="overflow-hidden rounded-lg border border-border-standard bg-surface-card shadow-sm" aria-labelledby="shipments-heading">
        <div className="flex flex-col justify-between gap-md border-b border-border-standard p-lg sm:flex-row sm:items-center">
          <h2 id="shipments-heading" className="sr-only">Shipment list</h2>
          <div className="flex max-w-full gap-xs overflow-x-auto rounded-button bg-surface-container-low p-xs">
            {filters.map((filter) => <button key={filter} className={`flex shrink-0 items-center gap-xs rounded-button px-md py-sm font-label-caps text-label-caps transition-colors ${activeFilter === filter ? 'bg-primary text-on-primary shadow-sm' : 'text-secondary hover:bg-surface-container hover:text-on-surface'}`} type="button" onClick={() => changeFilter(filter)} aria-pressed={activeFilter === filter}><span>{filter === 'All' ? 'All' : statusLabel(filter)}</span><span className={`rounded-lg px-xs py-[1px] text-[10px] ${activeFilter === filter ? 'bg-primary-container text-on-primary' : 'bg-surface-container text-secondary'}`}>{countFor(filter)}</span></button>)}
          </div>
          <button className="self-end rounded-lg p-xs text-secondary transition-colors hover:bg-surface-container-low hover:text-on-surface sm:self-auto" type="button" title="Refresh shipments" onClick={() => shipmentsQuery.refetch()}><Icon name="refresh" className="h-5 w-5" /></button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[980px] text-left">
            <thead><tr className="bg-surface-container-low font-label-caps text-label-caps uppercase tracking-wider text-secondary"><th className="px-xl py-md font-semibold">Tracking Number</th><th className="px-lg py-md font-semibold">Order</th><th className="px-lg py-md font-semibold">Customer</th><th className="px-lg py-md font-semibold">Created</th><th className="px-lg py-md font-semibold">Status</th><th className="px-xl py-md text-right font-semibold">Action</th></tr></thead>
            <tbody className="divide-y divide-border-subtle">
              {shipmentsQuery.isLoading ? <tr><td className="px-xl py-2xl text-center font-body-md text-body-md text-secondary" colSpan={6}>Loading shipments...</td></tr> : null}
              {shipmentsQuery.isError ? <tr><td className="px-xl py-2xl text-center font-body-md text-body-md text-status-error" colSpan={6}>Unable to load shipments. Refresh and try again.</td></tr> : null}
              {!shipmentsQuery.isLoading && !shipmentsQuery.isError && shipments.map((shipment) => <tr className="transition-colors hover:bg-surface-container-low/50" key={shipment.shipmentId}>
                <td className="px-xl py-lg"><Link className="font-product-title text-[15px] font-semibold text-primary hover:underline" href={`/operations/shipments/${shipment.shipmentId}`}>{shipment.trackingNumber}</Link></td>
                <td className="px-lg py-lg"><Link className="rounded-lg bg-surface-container px-sm py-xs font-label-caps text-label-caps font-medium text-on-surface hover:bg-surface-container-high" href={`/operations/fulfillment/${shipment.orderId}`}>#{shipment.orderId}</Link></td>
                <td className="px-lg py-lg"><div className="flex flex-col"><span className="font-status-pill text-status-pill font-semibold text-on-surface">{shipment.customerName}</span><span className="font-label-caps text-label-caps text-secondary">{shipment.destination}</span></div></td>
                <td className="whitespace-nowrap px-lg py-lg font-status-pill text-status-pill text-secondary">{formatDate(shipment.createdAt)}</td>
                <td className="px-lg py-lg"><span className={`inline-flex items-center gap-xs rounded-full px-sm py-xs font-status-pill text-status-pill font-medium ${statusClass(shipment.status)}`}><span className="h-1.5 w-1.5 rounded-full bg-current" />{statusLabel(shipment.status)}</span></td>
                <td className="px-xl py-lg text-right">{shipment.availableAction === 'None' ? <span className="px-md font-body-md text-outline">-</span> : <button className="rounded-button bg-primary px-md py-sm font-label-caps text-label-caps font-medium text-on-primary shadow-sm transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-60" type="button" disabled={commandPending} onClick={() => runAction(shipment.shipmentId, shipment.availableAction)}>{(inTransitMutation.isPending && inTransitMutation.variables === shipment.shipmentId) ? 'Updating...' : (deliveredMutation.isPending && deliveredMutation.variables === shipment.shipmentId) ? 'Updating...' : shipment.availableAction === 'MarkInTransit' ? 'Mark In Transit' : 'Mark Delivered'}</button>}</td>
              </tr>)}
            </tbody>
          </table>
        </div>
        {commandError ? <p className="border-t border-border-standard px-xl py-md font-status-pill text-status-pill text-status-error" role="alert">{commandErrorMessage(commandError)}</p> : null}
        {!shipmentsQuery.isLoading && !shipmentsQuery.isError && shipments.length === 0 ? <p className="p-2xl text-center font-body-md text-body-md text-secondary">No shipments match the current filters.</p> : null}
        <div className="flex flex-col justify-between gap-md border-t border-border-standard bg-surface-container-low px-xl py-md font-label-caps text-label-caps text-secondary sm:flex-row sm:items-center"><span>Showing <strong className="text-on-surface">{shipments.length ? `${(page - 1) * pageSize + 1}-${(page - 1) * pageSize + shipments.length}` : '0'}</strong> of <strong className="text-on-surface">{totalCount}</strong> shipments</span><div className="flex items-center gap-xs" aria-label="Pagination"><button className="rounded-lg p-xs text-secondary disabled:text-outline/40" type="button" disabled={page === 1 || shipmentsQuery.isLoading} onClick={() => setPage((current) => current - 1)} title="Previous page"><Icon name="chevron-left" className="h-[18px] w-[18px]" /></button>{Array.from({ length: pageCount }, (_, index) => index + 1).map((pageNumber) => <button key={pageNumber} className={`flex h-7 w-7 items-center justify-center rounded-lg font-label-caps text-label-caps font-semibold ${pageNumber === page ? 'bg-primary text-on-primary' : 'text-secondary hover:bg-surface-card hover:text-on-surface'}`} type="button" aria-current={pageNumber === page ? 'page' : undefined} onClick={() => setPage(pageNumber)}>{pageNumber}</button>)}<button className="rounded-lg p-xs text-secondary disabled:text-outline/40" type="button" disabled={page >= pageCount || shipmentsQuery.isLoading} onClick={() => setPage((current) => current + 1)} title="Next page"><Icon name="chevron-right" className="h-[18px] w-[18px]" /></button></div></div>
      </section>
    </main>
  );
}

function Metric({ label, value, icon, tone = 'default' }: { label: string; value: number; icon: 'inventory' | 'assignment' | 'shopping-bag' | 'precision'; tone?: 'default' | 'success' }) {
  return <article className="relative flex min-h-[147px] flex-col justify-between overflow-hidden rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm"><div className="flex items-center justify-between"><span className="font-label-caps text-label-caps uppercase tracking-wider text-secondary">{label}</span><Icon name={icon} className={tone === 'success' ? 'h-5 w-5 text-status-success' : 'h-5 w-5 text-secondary'} /></div><span className="font-display text-display tracking-tight text-primary">{value}</span><div className="h-1 w-full overflow-hidden rounded-full bg-surface-container"><div className={`h-full w-full rounded-full ${tone === 'success' ? 'bg-status-success' : 'bg-primary'}`} /></div></article>;
}