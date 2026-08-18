import './globals.css';

export const metadata = { title: 'Luna Shop', description: 'Commerce and logistics simulation' };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
