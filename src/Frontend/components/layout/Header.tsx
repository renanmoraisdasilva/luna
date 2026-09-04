'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import Icon from './Icon';

const navigationLinks = [
  { label: 'Shop', href: '/shop' },
  { label: 'Orders', href: '/orders' },
  { label: 'Swagger', href: '/swagger' },
];

const utilityLinks = [
  { label: 'Account', icon: 'account' as const, href: '/account' },
  { label: 'Cart', icon: 'cart' as const, href: '/cart' },
];

export default function Header() {
  const pathname = usePathname();

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
            </Link>;
          })}
        </div>
      </div>
    </header>
  );
}
