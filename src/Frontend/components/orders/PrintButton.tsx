'use client';

export default function PrintButton() {
  return <button className="h-control rounded-button border border-outline bg-surface-card px-lg py-sm font-label-caps text-label-caps uppercase text-on-surface shadow-sm transition-colors hover:border-outline-variant" type="button" onClick={() => window.print()}>Download invoice</button>;
}
