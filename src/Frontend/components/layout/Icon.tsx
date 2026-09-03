type IconName = 'cart' | 'search' | 'expand-more';

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

  return <svg {...commonProps}><circle cx="9" cy="20" r="1" /><circle cx="18" cy="20" r="1" /><path d="M3 4h2l2.2 10.2a2 2 0 0 0 2 1.6h7.9a2 2 0 0 0 1.9-1.5L20.5 8H6" /></svg>;
}
