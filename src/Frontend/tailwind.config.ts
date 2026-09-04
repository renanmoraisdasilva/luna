import type { Config } from 'tailwindcss';

const config: Config = {
  content: ['./app/**/*.{js,ts,jsx,tsx,mdx}', './components/**/*.{js,ts,jsx,tsx,mdx}'],
  theme: {
    extend: {
      colors: {
        background: '#fbf9fa',
        'surface-card': '#ffffff',
        'surface-container': '#efedef',
        'surface-container-high': '#eae7e9',
        'surface-container-low': '#f5f3f5',
        'surface-container-lowest': '#ffffff',
        'surface-base': '#f8fafc',
        'surface-variant': '#e4e2e4',
        primary: '#0f1e31',
        'primary-container': '#253347',
        'on-primary': '#ffffff',
        'on-primary-container': '#8d9bb3',
        secondary: '#515f74',
        'on-background': '#1b1b1d',
        'on-surface': '#1b1b1d',
        'on-surface-variant': '#44474c',
        'status-success': '#10b981',
        'status-error': '#dc2626',
        'border-standard': '#e2e8f0',
        'border-subtle': '#f1f5f9',
        'outline': '#75777d',
        'outline-variant': '#c5c6cd',
      },
      borderRadius: {
        button: '0.25rem',
        lg: '0.25rem',
        xl: '0.5rem',
      },
      spacing: {
        xs: '4px',
        sm: '8px',
        md: '12px',
        lg: '16px',
        xl: '24px',
        '2xl': '32px',
        '3xl': '48px',
        '4xl': '64px',
        control: '40px',
      },
      fontFamily: {
        display: ['Inter', 'sans-serif'],
        'label-caps': ['Inter', 'sans-serif'],
        'status-pill': ['Inter', 'sans-serif'],
        'headline-lg': ['Inter', 'sans-serif'],
        'product-title': ['Inter', 'sans-serif'],
        'body-md': ['Inter', 'sans-serif'],
      },
      fontSize: {
        display: ['48px', { lineHeight: '56px', letterSpacing: '-0.03em', fontWeight: '700' }],
        'label-caps': ['12px', { lineHeight: '16px', letterSpacing: '0.05em', fontWeight: '600' }],
        'status-pill': ['13px', { lineHeight: '16px', fontWeight: '500' }],
        'headline-lg': ['32px', { lineHeight: '40px', letterSpacing: '-0.02em', fontWeight: '700' }],
        'headline-lg-mobile': ['28px', { lineHeight: '36px', letterSpacing: '-0.02em', fontWeight: '700' }],
        'product-title': ['20px', { lineHeight: '28px', letterSpacing: '-0.01em', fontWeight: '600' }],
        'body-md': ['16px', { lineHeight: '24px', fontWeight: '400' }],
      },
    },
  },
  plugins: [],
};

export default config;
