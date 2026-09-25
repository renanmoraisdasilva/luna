import OperationsSidebar from '../../components/operations/OperationsSidebar';

export default function OperationsLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <div className="-mt-4xl min-h-screen bg-background text-on-surface">
      <OperationsSidebar />
      <main className="min-h-screen lg:ml-64">{children}</main>
    </div>
  );
}