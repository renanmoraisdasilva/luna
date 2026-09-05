import LoginForm from '../../components/auth/LoginForm';

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ returnUrl?: string | string[] }> }) {
  const params = await searchParams;
  const requestedReturnUrl = Array.isArray(params.returnUrl) ? params.returnUrl[0] : params.returnUrl;
  const returnUrl = requestedReturnUrl?.startsWith('/') && !requestedReturnUrl.startsWith('//') ? requestedReturnUrl : '/';

  return (
    <main className="flex min-h-[calc(100vh-64px)] items-center justify-center bg-surface px-lg py-3xl selection:bg-secondary-container selection:text-on-secondary-container">
      <section className="w-full max-w-lg rounded-xl border border-border-standard bg-surface-card p-2xl shadow-[0_2px_12px_rgba(0,0,0,0.03)]">
        <div className="relative flex flex-col items-center py-8 md:py-12">
          <div className="pointer-events-none absolute -left-12 -top-12 -z-0 h-64 w-64 rounded-full bg-secondary-fixed/30 blur-3xl" />
          <div className="pointer-events-none absolute -bottom-8 -right-8 -z-0 h-56 w-56 rounded-full bg-primary-fixed/20 blur-3xl" />
          <div className="relative z-10 mb-8 flex max-w-sm flex-col items-center text-center">
            <div className="mb-6 font-display text-[64px] font-bold leading-none tracking-tight text-primary">Luna</div>
            <h1 className="font-headline-lg text-headline-lg tracking-tight text-primary">Log in</h1>
            <p className="mt-sm font-body-md text-body-md text-secondary">Welcome back. Enter your credentials to access your account, preferences, and orders.</p>
          </div>
          <LoginForm returnUrl={returnUrl} />
          <nav className="mt-8 flex items-center gap-xl font-label-caps text-label-caps uppercase tracking-wider text-outline" aria-label="Account help">
            <a className="transition-colors hover:text-primary" href="#privacy">Privacy Policy</a>
            <span aria-hidden="true">•</span>
            <a className="transition-colors hover:text-primary" href="#terms">Terms of Service</a>
            <span aria-hidden="true">•</span>
            <a className="transition-colors hover:text-primary" href="#help">Support</a>
          </nav>
        </div>
      </section>
    </main>
  );
}
