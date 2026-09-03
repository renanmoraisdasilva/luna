'use client';

import type { FormEvent } from 'react';
import { useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import Icon from '../layout/Icon';
import type { CatalogCategory } from '../../types/catalog';
import { createShopUrl } from '../../lib/catalog/url';

type CatalogToolbarProps = {
  search?: string;
  category?: string;
  categories?: CatalogCategory[];
};

export default function CatalogToolbar({
  search = '',
  category = '',
  categories = [],
}: CatalogToolbarProps) {
  const [searchInput, setSearchInput] = useState(search);
  const pathname = usePathname();
  const router = useRouter();
  const currentQuery = useSearchParams().toString();

  function categoryUrl(categoryValue: string) {
    return createShopUrl(pathname, currentQuery, { category: categoryValue, page: '1' });
  }

  function updateUrl(changes: Parameters<typeof createShopUrl>[2]) {
    router.push(createShopUrl(pathname, currentQuery, changes));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    updateUrl({ search: searchInput.trim(), page: '1' });
  }

  return (
    <section className="flex flex-col items-start justify-between gap-lg border-b border-border-standard pb-lg lg:flex-row lg:items-center">
      <div className="flex w-full flex-col items-start gap-lg md:flex-row md:items-center lg:w-auto">
        <form className="relative w-full md:w-[280px]" onSubmit={handleSubmit}>
          <Icon name="search" className="absolute left-md top-1/2 h-5 w-5 -translate-y-1/2 text-on-surface-variant" />
          <input key={search} className="h-control w-full rounded border border-outline-variant bg-surface-container-lowest py-sm pl-3xl pr-md font-body-md text-body-md text-on-surface outline-none transition-shadow focus:border-primary focus:ring-1 focus:ring-primary" placeholder="Search products..." type="search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} />
        </form>
        <div className="hide-scrollbar flex w-full gap-sm overflow-x-auto pb-xs md:w-auto md:pb-0">
          <Link
            className={!category
              ? 'h-control whitespace-nowrap rounded-button bg-primary px-lg py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:opacity-90'
              : 'h-control whitespace-nowrap rounded-button bg-surface-container px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface transition-colors hover:bg-surface-container-high'}
            href={categoryUrl('')}
          >
            All
          </Link>
          {categories.map((item) => (
            <Link
              className={category === item.slug
                ? 'h-control whitespace-nowrap rounded-button bg-primary px-lg py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:opacity-90'
                : 'h-control whitespace-nowrap rounded-button bg-surface-container px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface transition-colors hover:bg-surface-container-high'}
              key={item.id}
              href={categoryUrl(item.slug)}
            >
              {item.name}
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
