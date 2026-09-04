import '@testing-library/jest-dom/vitest';
import React from 'react';
import { vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) =>
    React.createElement('a', { ...props, href }, children),
}));

vi.mock('next/image', () => ({
  default: ({ src, alt, fill: _fill, priority: _priority, ...props }: React.ImgHTMLAttributes<HTMLImageElement> & { src: string; fill?: boolean; priority?: boolean }) =>
    React.createElement('img', { ...props, src, alt }),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/shop',
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));
