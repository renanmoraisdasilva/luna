import { redirect } from 'next/navigation';
import CartView from '../../../components/cart/CartView';
import Footer from '../../../components/layout/Footer';
import { getCurrentUserServer } from '../../../lib/auth-server';

export default async function CartPage() {
  const currentUser = await getCurrentUserServer();

  if (!currentUser) {
    redirect('/login?returnUrl=/cart');
  }

  return (
    <>
      <CartView />
      <Footer />
    </>
  );
}
