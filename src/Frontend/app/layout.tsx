import './globals.css';
import Header from '../components/layout/Header';
import QueryProvider from '../components/providers/QueryProvider';

export const metadata = { title: 'Luna Shop', description: 'Commerce and logistics simulation' };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-background pt-4xl font-body-md text-body-md text-on-background">
        <QueryProvider><Header />{children}</QueryProvider>
      </body>
    </html>
  )
}
