import Link from 'next/link';
import { createShopUrl } from '../../lib/catalog/url';

type PaginationProps = {
  page?: number;
  pageSize?: number;
  totalCount?: number;
  pathname?: string;
  currentQuery?: string;
};

export default function Pagination({
  page = 1,
  pageSize = 12,
  totalCount = 12,
  pathname = '/shop',
  currentQuery = '',
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const canGoPrevious = page > 1;
  const canGoNext = page < totalPages;
  const buttonClass = 'inline-flex h-control items-center justify-center rounded-button border border-outline-variant bg-surface-container-lowest px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface transition-colors hover:bg-surface-container';
  const disabledClass = `${buttonClass} cursor-not-allowed opacity-50`;

  return (
    <nav className="flex items-center justify-center gap-sm pt-lg" aria-label="Pagination">
      {canGoPrevious ? (
        <Link className={buttonClass} href={createShopUrl(pathname, currentQuery, { page: String(page - 1) })}>Previous</Link>
      ) : (
        <span className={disabledClass} aria-disabled="true">Previous</span>
      )}
      <span className="px-md font-body-md text-body-md text-on-surface-variant">Page {page}{totalPages > 1 ? ` of ${totalPages}` : ''}</span>
      {canGoNext ? (
        <Link className={buttonClass} href={createShopUrl(pathname, currentQuery, { page: String(page + 1) })}>Next</Link>
      ) : (
        <span className={disabledClass} aria-disabled="true">Next</span>
      )}
    </nav>
  );
}
