import RegisterForm from '../../components/auth/RegisterForm';

export default function RegisterPage() {
  return (
    <main className="flex min-h-[calc(100vh-64px)] items-center justify-center bg-surface px-lg py-3xl selection:bg-secondary-container selection:text-on-secondary-container">
      <section className="w-full max-w-lg rounded-xl border border-border-standard bg-surface-card p-2xl shadow-[0_2px_12px_rgba(0,0,0,0.03)]">
        <div className="relative flex flex-col items-center py-8 md:py-12">
          <div className="pointer-events-none absolute -left-12 -top-12 -z-0 h-64 w-64 rounded-full bg-secondary-fixed/30 blur-3xl" />
          <div className="pointer-events-none absolute -bottom-8 -right-8 -z-0 h-56 w-56 rounded-full bg-primary-fixed/20 blur-3xl" />
          <div className="relative z-10 mb-8 flex max-w-sm flex-col items-center text-center">
            <div className="mb-6 font-display text-[64px] font-bold leading-none tracking-tight text-primary">Luna</div>
          </div>
          <RegisterForm />
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
