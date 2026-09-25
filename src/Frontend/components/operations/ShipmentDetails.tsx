'use client';

import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import Icon from '../layout/Icon';
import { markShipmentDelivered, markShipmentInTransit } from '../../lib/api/orders';
import { getShipment, type ShipmentStatus } from '../../lib/api/shipping';

function statusLabel(status: ShipmentStatus) {
  return status === 'InTransit' ? 'In Transit' : status;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function commandErrorMessage(error: unknown) {
  if (axios.isAxiosError<{ message?: string }>(error)) return error.response?.data?.message ?? 'The shipment command could not be completed.';
  return 'The shipment command could not be completed.';
}

export default function ShipmentDetails({ shipmentId }: { shipmentId: string }) {
  const queryClient = useQueryClient();
  const shipmentQuery = useQuery({ queryKey: ['shipment', shipmentId], queryFn: () => getShipment(shipmentId) });
  const inTransitMutation = useMutation({ mutationFn: markShipmentInTransit, onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['shipment', shipmentId] }); queryClient.invalidateQueries({ queryKey: ['shipments'] }); } });
  const deliveredMutation = useMutation({ mutationFn: markShipmentDelivered, onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['shipment', shipmentId] }); queryClient.invalidateQueries({ queryKey: ['shipments'] }); } });

  if (shipmentQuery.isLoading) return <main className="mx-auto flex w-full max-w-[1280px] flex-grow items-center justify-center px-lg py-3xl font-body-md text-body-md text-secondary">Loading shipment...</main>;
  if (shipmentQuery.isError || !shipmentQuery.data) return <main className="mx-auto flex w-full max-w-[1280px] flex-grow items-center justify-center px-lg py-3xl font-body-md text-body-md text-status-error">Unable to load this shipment.</main>;

  const shipment = shipmentQuery.data;
  const actionPending = inTransitMutation.isPending || deliveredMutation.isPending;
  const actionError = inTransitMutation.error ?? deliveredMutation.error;
  const address = [shipment.recipient.addressLine1, shipment.recipient.addressLine2, `${shipment.recipient.city}, ${shipment.recipient.stateOrProvince} ${shipment.recipient.postalCode}`, shipment.recipient.country].filter(Boolean);

  function runAction() {
    if (shipment.availableAction === 'MarkInTransit') inTransitMutation.mutate(shipment.shipmentId);
    if (shipment.availableAction === 'MarkDelivered') deliveredMutation.mutate(shipment.shipmentId);
  }

  return <main className="mx-auto flex w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
    <div className="flex items-center gap-xs font-label-caps text-label-caps text-secondary"><Link className="hover:text-primary" href="/operations/shipments">Shipments</Link><Icon name="chevron-right" className="h-3.5 w-3.5" /><span className="font-semibold text-primary">{shipment.trackingNumber}</span></div>
    <section className="flex flex-col justify-between gap-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm md:flex-row md:items-center"><div><div className="flex flex-wrap items-center gap-md"><h1 className="font-headline-lg text-headline-lg-mobile tracking-tight text-primary md:text-headline-lg">Shipment {shipment.trackingNumber}</h1><span className="rounded-full bg-primary/10 px-md py-xs font-label-caps text-label-caps uppercase text-primary">{statusLabel(shipment.status)}</span></div><p className="mt-xs font-body-md text-body-md text-secondary">Outbound delivery for <Link className="font-semibold text-primary hover:underline" href={`/operations/fulfillment/${shipment.orderId}`}>Order #{shipment.orderId}</Link></p></div>{shipment.availableAction !== 'None' ? <button className="inline-flex h-control items-center justify-center gap-sm rounded-button bg-primary px-xl py-sm font-label-caps text-label-caps font-medium text-on-primary shadow-sm transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-60" type="button" disabled={actionPending} onClick={runAction}>{actionPending ? 'Updating...' : shipment.availableAction === 'MarkInTransit' ? 'Mark In Transit' : 'Mark Delivered'}</button> : <span className="font-label-caps text-label-caps uppercase text-status-success">Lifecycle complete</span>}</section>
    {actionError ? <p className="rounded-lg border border-error-container bg-error-container px-lg py-md font-status-pill text-status-pill text-status-error" role="alert">{commandErrorMessage(actionError)}</p> : null}
    <div className="grid grid-cols-1 gap-xl lg:grid-cols-12">
      <section className="space-y-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm lg:col-span-7" aria-labelledby="shipment-events-heading"><div><h2 id="shipment-events-heading" className="font-product-title text-product-title text-primary">Milestones &amp; custody log</h2><p className="mt-xs font-label-caps text-label-caps text-secondary">Authoritative tracking events for this shipment.</p></div><ol className="relative space-y-lg border-l-2 border-surface-container-highest pl-xl">{shipment.trackingEvents.map((event, index) => <li className="relative" key={event.id}><span className="absolute -left-[29px] top-xs h-4 w-4 rounded-full border-4 border-white bg-primary shadow-sm" aria-hidden="true" /><div className="rounded-lg bg-surface-base p-lg"><div className="flex flex-col justify-between gap-xs sm:flex-row sm:items-center"><span className="font-product-title text-product-title text-primary">{statusLabel(event.status)}</span><time className="font-label-caps text-label-caps text-secondary" dateTime={event.occurredAt}>{formatDate(event.occurredAt)}</time></div><span className="mt-xs block font-label-caps text-label-caps uppercase text-secondary">{index === 0 ? 'Shipment created' : index === 1 ? 'Shipment movement recorded' : 'Delivery completed'}</span></div></li>)}</ol></section>
      <aside className="space-y-xl lg:col-span-5"><section className="space-y-lg rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="destination-heading"><div className="flex items-center justify-between"><div><h2 id="destination-heading" className="font-product-title text-product-title text-primary">Destination details</h2><p className="mt-xs font-label-caps text-label-caps text-secondary">Immutable shipment recipient snapshot.</p></div><Icon name="location" className="h-5 w-5 text-secondary" /></div><div className="rounded-lg bg-surface-base p-lg"><p className="font-product-title text-product-title font-semibold text-primary">{shipment.recipient.fullName}</p><address className="mt-md not-italic font-body-md text-body-md text-on-surface-variant">{address.map((line) => <span className="block" key={line}>{line}</span>)}</address></div></section><section className="space-y-md rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm" aria-labelledby="shipment-meta-heading"><h2 id="shipment-meta-heading" className="font-product-title text-product-title text-primary">Shipment metadata</h2><div className="space-y-sm font-body-md text-body-md"><div className="flex justify-between gap-md"><span className="text-secondary">Order</span><Link className="truncate font-semibold text-primary hover:underline" href={`/operations/fulfillment/${shipment.orderId}`}>#{shipment.orderId}</Link></div><div className="flex justify-between gap-md"><span className="text-secondary">Customer ID</span><span className="truncate text-on-surface">{shipment.customerId}</span></div><div className="flex justify-between gap-md"><span className="text-secondary">Created</span><span className="text-on-surface">{formatDate(shipment.createdAt)}</span></div>{shipment.deliveredAt ? <div className="flex justify-between gap-md"><span className="text-secondary">Delivered</span><span className="text-on-surface">{formatDate(shipment.deliveredAt)}</span></div> : null}</div></section></aside>
    </div>
  </main>;
}