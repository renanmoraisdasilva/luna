type IconName = 'account' | 'cart' | 'search' | 'expand-more' | 'add' | 'remove' | 'delete' | 'chevron-right' | 'shopping-bag';

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

  return <svg {...commonProps}><circle cx="9" cy="20" r="1" /><circle cx="18" cy="20" r="1" /><path d="M3 4h2l2.2 10.2a2 2 0 0 0 2 1.6h7.9a2 2 0 0 0 1.9-1.5L20.5 8H6" /></svg>;
}
