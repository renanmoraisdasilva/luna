'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import Icon from './Icon';
import { getCart } from '../../lib/api/orders';
import { useCurrentUser } from '../../lib/queries/auth';

const navigationLinks = [
  { label: 'Shop', href: '/shop' },
  { label: 'Orders', href: '/orders' },
  { label: 'Operations', href: '/operations/fulfillment' },
];

const utilityLinks = [
  { label: 'Account', icon: 'account' as const, href: '/account' },
  { label: 'Cart', icon: 'cart' as const, href: '/cart' },
];

export default function Header() {
  const pathname = usePathname();
  const userQuery = useCurrentUser();
  const cartQuery = useQuery({
    queryKey: ['cart'],
    queryFn: getCart,
    retry: false,
    enabled: Boolean(userQuery.data),
  });
  const cartItemCount = userQuery.data
    ? cartQuery.data?.items.reduce((total, item) => total + item.quantity, 0) ?? 0
    : 0;

  return (
    <header className="fixed top-0 z-50 flex h-4xl w-full justify-center border-b border-[#e5e7eb] bg-[#fbf9fa] shadow-[0_1px_3px_0_rgba(0,0,0,0.05)]">
      <div className="relative mx-auto flex h-full w-full max-w-[1280px] items-center justify-between px-lg md:px-xl">
        <Link className="font-display text-display font-bold text-primary" href="/shop">
          Luna
        </Link>
        <nav className="absolute left-1/2 hidden h-full -translate-x-1/2 items-center gap-2xl md:flex">
          {navigationLinks.map((link) => (
            <Link
              className={pathname === link.href || pathname.startsWith(`${link.href}/`)
                ? 'flex h-full items-center border-b-2 border-on-background pt-xs font-bold text-on-background'
                : 'flex h-full items-center text-[#6b7280] transition-colors duration-200 hover:text-[#253347]'}
              href={link.href}
              key={link.href}
            >
              {link.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-sm">
          {utilityLinks.map((link) => {
            return <Link className={'relative flex items-center justify-center rounded-button p-xs text-primary transition-colors hover:bg-surface-container-low'} href={link.href} aria-label={link.label} key={link.label}>
              <Icon name={link.icon} />
              {link.label === 'Cart' && cartItemCount > 0 ? <span className="absolute -right-1 -top-1 flex min-h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1 font-label-caps text-[10px] leading-none text-on-primary" aria-hidden="true">{cartItemCount}</span> : null}
            </Link>;
          })}
        </div>
      </div>
    </header>
  );
}
