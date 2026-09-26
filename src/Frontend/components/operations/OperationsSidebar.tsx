'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useSyncExternalStore } from 'react';
import Icon from '../layout/Icon';

const navigationLinks = [
  { label: 'Fulfillment', href: '/operations/fulfillment', icon: 'inventory' as const },
  { label: 'Shipments', href: '/operations/shipments', icon: 'shopping-bag' as const },
];
const configuredSignozUrl = process.env.NEXT_PUBLIC_SIGNOZ_URL;
const noopSubscribe = () => () => {};
const getSignozUrl = () => `${window.location.protocol}//${window.location.hostname}:8080`;

const toolLinks = [
  { label: 'Swagger', href: '/swagger', icon: 'description' as const },
  { label: 'SigNoz', icon: 'monitoring' as const, external: true },
];

export default function OperationsSidebar() {
  const pathname = usePathname() ?? '';
  const browserSignozUrl = useSyncExternalStore(noopSubscribe, getSignozUrl, () => 'http://localhost:8080');
  const signozUrl = configuredSignozUrl || browserSignozUrl;

  const resolvedToolLinks = toolLinks.map((link) => ({ ...link, href: link.href ?? signozUrl }));

  return (
    <>
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-64 flex-col justify-between border-r border-border-standard bg-surface-card shadow-sm lg:flex">
        <div className="flex flex-col">
          <div className="flex h-4xl items-center justify-between bg-primary px-lg">
            <div className="flex items-center gap-sm">
              <span className="h-2 w-2 rounded-full bg-status-success" aria-hidden="true" />
              <span className="font-product-title text-product-title tracking-tight text-on-primary">Luna Ops</span>
            </div>
            <span className="rounded-lg bg-primary-container px-xs py-xs font-label-caps text-label-caps uppercase text-on-primary-container">v2.6</span>
          </div>
          <div className="px-lg pb-sm pt-lg">
            <span className="font-label-caps text-label-caps uppercase tracking-wider text-secondary">Operations Core</span>
          </div>
          <nav className="flex flex-col gap-xs px-lg" aria-label="Operations">
            {navigationLinks.map((link) => {
              const isActive = pathname === link.href || pathname.startsWith(`${link.href}/`);
              return (
                <Link
                  className={`flex items-center gap-md rounded-button px-md py-sm font-status-pill text-status-pill transition-colors ${isActive ? 'bg-primary-container text-on-primary' : 'text-on-surface-variant hover:bg-surface-container-low hover:text-on-surface'}`}
                  href={link.href}
                  aria-current={isActive ? 'page' : undefined}
                  key={link.href}
                >
                  <Icon name={link.icon} className="h-5 w-5" />
                  <span>{link.label}</span>
                </Link>
              );
            })}
          </nav>
          <div className="mt-lg border-t border-border-subtle px-lg pt-lg">
            <span className="font-label-caps text-label-caps uppercase tracking-wider text-secondary">Developer tools</span>
            <nav className="mt-sm flex flex-col gap-xs" aria-label="Developer tools">
              {resolvedToolLinks.map((link) => (
                <Link className="flex items-center justify-between rounded-button px-md py-sm font-status-pill text-status-pill text-on-surface-variant transition-colors hover:bg-surface-container-low hover:text-on-surface" href={link.href} key={link.label} target={link.external ? '_blank' : undefined} rel={link.external ? 'noreferrer' : undefined}>
                  <span className="flex items-center gap-md"><Icon name={link.icon} className="h-5 w-5" />{link.label}</span>
                  {link.external ? <Icon name="external-link" className="h-3.5 w-3.5" /> : null}
                </Link>
              ))}
            </nav>
          </div>
        </div>
        <div className="flex flex-col gap-lg border-t border-border-subtle bg-surface-container-lowest p-lg">
          <Link className="inline-flex items-center justify-between font-label-caps text-label-caps text-secondary transition-colors hover:text-primary" href="/shop">
            <span className="flex items-center gap-xs"><Icon name="shopping-bag" className="h-4 w-4" />Storefront</span>
            <Icon name="external-link" className="h-3.5 w-3.5" />
          </Link>
          <div className="flex items-center gap-sm rounded-button bg-surface-container-low p-sm">
            <span className="h-2 w-2 rounded-full bg-status-success" aria-hidden="true" />
            <div className="flex min-w-0 flex-col">
              <span className="truncate font-label-caps text-label-caps text-on-surface">Operations Staff</span>
              <span className="truncate font-label-caps text-label-caps text-on-surface-variant">Active session</span>
            </div>
          </div>
        </div>
      </aside>

      <div className="flex items-center justify-between border-b border-border-standard bg-primary px-lg py-md lg:hidden">
        <span className="font-product-title text-product-title text-on-primary">Luna Ops</span>
        <nav className="flex items-center gap-xs" aria-label="Operations">
          {navigationLinks.map((link) => {
            const isActive = pathname === link.href || pathname.startsWith(`${link.href}/`);
            return <Link className={`rounded-button px-sm py-xs font-label-caps text-label-caps ${isActive ? 'bg-primary-container text-on-primary' : 'text-on-primary-container'}`} href={link.href} aria-current={isActive ? 'page' : undefined} key={link.href}>{link.label}</Link>;
          })}
          {resolvedToolLinks.map((link) => <Link className="rounded-button px-sm py-xs font-label-caps text-label-caps text-on-primary-container" href={link.href} key={link.label} target={link.external ? '_blank' : undefined} rel={link.external ? 'noreferrer' : undefined}>{link.label}</Link>)}
        </nav>
      </div>
    </>
  );
}
