const footerLinks = ['Terms of Service', 'Privacy Policy', 'Contact Us', 'Return Policy'];

export default function Footer() {
  return (
    <footer className="w-full border-t border-border-standard bg-surface-container-low">
      <div className="mx-auto flex w-full max-w-[1280px] flex-col items-center justify-between gap-lg px-lg py-2xl md:flex-row md:px-xl">
        <div className="font-label-caps text-label-caps font-bold text-secondary">
          © 2024 Luna. Simulated platform for demonstration purposes.
        </div>
        <nav className="flex flex-wrap justify-center gap-lg font-status-pill text-status-pill">
          {footerLinks.map((item) => (
            <a className="cursor-pointer text-secondary transition-colors duration-200 hover:text-primary" href="#" key={item}>
              {item}
            </a>
          ))}
        </nav>
      </div>
    </footer>
  );
}
