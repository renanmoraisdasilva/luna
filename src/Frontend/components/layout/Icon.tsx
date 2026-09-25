type IconName = 'account' | 'cart' | 'search' | 'expand-more' | 'add' | 'remove' | 'delete' | 'chevron-right' | 'chevron-left' | 'shopping-bag' | 'description' | 'monitoring' | 'inventory' | 'assignment' | 'precision' | 'sync-problem' | 'refresh' | 'play' | 'replay' | 'info' | 'download' | 'copy' | 'location' | 'external-link';

export default function Icon({ name, className = '' }: { name: IconName; className?: string }) {
  const commonProps = {
    className: `inline-block shrink-0 ${className}`,
    width: 24,
    height: 24,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
  };

  if (name === 'search') {
    return <svg {...commonProps}><circle cx="11" cy="11" r="6.5" /><path d="m16 16 4.5 4.5" /></svg>;
  }

  if (name === 'expand-more') {
    return <svg {...commonProps}><path d="m6 9 6 6 6-6" /></svg>;
  }

  if (name === 'account') {
    return <svg {...commonProps}><circle cx="12" cy="7.5" r="3.5" /><path d="M5 20c.7-3.7 3.1-5.8 7-5.8s6.3 2.1 7 5.8" /></svg>;
  }

  if (name === 'add') {
    return <svg {...commonProps}><path d="M12 5v14M5 12h14" /></svg>;
  }

  if (name === 'remove') {
    return <svg {...commonProps}><path d="M5 12h14" /></svg>;
  }

  if (name === 'delete') {
    return <svg {...commonProps}><path d="M4 7h16M9 7V4h6v3M7 7l1 13h8l1-13M10 11v5M14 11v5" /></svg>;
  }

  if (name === 'chevron-right') {
    return <svg {...commonProps}><path d="m9 6 6 6-6 6" /></svg>;
  }

  if (name === 'shopping-bag') {
    return <svg {...commonProps}><path d="M6 8h12l1 12H5L6 8Z" /><path d="M9 8a3 3 0 0 1 6 0" /></svg>;
  }

  if (name === 'chevron-left') {
    return <svg {...commonProps}><path d="m15 6-6 6 6 6" /></svg>;
  }

  if (name === 'description') {
    return <svg {...commonProps}><path d="M6 3.5h8l4 4V20.5H6z" /><path d="M14 3.5v4h4M9 12h6M9 16h6" /></svg>;
  }

  if (name === 'monitoring') {
    return <svg {...commonProps}><path d="M4 18V6M4 18h16" /><path d="m7 14 3-4 3 2 4-6" /></svg>;
  }

  if (name === 'inventory') {
    return <svg {...commonProps}><path d="m4 7 8-4 8 4-8 4-8-4Z" /><path d="M4 7v10l8 4 8-4V7M12 11v10" /></svg>;
  }

  if (name === 'assignment') {
    return <svg {...commonProps}><path d="M8 5.5h8M9 3.5h6v4H9zM6 5.5H4v15h16v-15h-2" /><path d="m8 14 2.5 2.5L16 11" /></svg>;
  }

  if (name === 'precision') {
    return <svg {...commonProps}><path d="M12 5v14M5 12h14M7 7l10 10M17 7 7 17" /><circle cx="12" cy="12" r="3.5" /></svg>;
  }

  if (name === 'sync-problem') {
    return <svg {...commonProps}><path d="M18 8a7 7 0 0 0-11.5-2L5 7.5M6 4v3.5h3.5M6 16a7 7 0 0 0 11.5 2l1.5-1.5M18 20v-3.5h-3.5" /><path d="M12 9v3M12 15h.01" /></svg>;
  }

  if (name === 'refresh') {
    return <svg {...commonProps}><path d="M20 11a8 8 0 0 0-14.8-4L3 10M3 5v5h5M4 13a8 8 0 0 0 14.8 4L21 14M21 19v-5h-5" /></svg>;
  }

  if (name === 'play') {
    return <svg {...commonProps}><path d="m8 5 11 7-11 7V5Z" /></svg>;
  }

  if (name === 'replay') {
    return <svg {...commonProps}><path d="M4 10a8 8 0 1 1 2.5 7.3M4 4v6h6" /></svg>;
  }

  if (name === 'info') {
    return <svg {...commonProps}><circle cx="12" cy="12" r="9" /><path d="M12 10v6M12 7h.01" /></svg>;
  }

  if (name === 'download') {
    return <svg {...commonProps}><path d="M12 3v12M7 10l5 5 5-5M4 20h16" /></svg>;
  }

  if (name === 'copy') {
    return <svg {...commonProps}><rect x="8" y="8" width="11" height="11" rx="1.5" /><path d="M16 8V5H5v11h3" /></svg>;
  }

  if (name === 'location') {
    return <svg {...commonProps}><path d="M19 10c0 5-7 10-7 10S5 15 5 10a7 7 0 1 1 14 0Z" /><circle cx="12" cy="10" r="2.2" /></svg>;
  }

  if (name === 'external-link') {
    return <svg {...commonProps}><path d="M14 5h5v5M19 5l-8 8" /><path d="M17 13v5H5V6h5" /></svg>;
  }

  return <svg {...commonProps}><circle cx="9" cy="20" r="1" /><circle cx="18" cy="20" r="1" /><path d="M3 4h2l2.2 10.2a2 2 0 0 0 2 1.6h7.9a2 2 0 0 0 1.9-1.5L20.5 8H6" /></svg>;
}
