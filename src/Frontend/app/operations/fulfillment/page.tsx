import { redirect } from 'next/navigation';
import Footer from '../../../components/layout/Footer';
import FulfillmentQueue from '../../../components/operations/FulfillmentQueue';
import { getCurrentUserServer } from '../../../lib/auth-server';

export default async function FulfillmentPage() {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) redirect('/login?returnUrl=/operations/fulfillment');

  return (
    <>
      <FulfillmentQueue />
      <Footer />
    </>
  );
}
