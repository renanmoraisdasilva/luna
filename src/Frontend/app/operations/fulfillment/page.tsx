import { redirect } from 'next/navigation';
import FulfillmentQueue from '../../../components/operations/FulfillmentQueue';
import { getCurrentUserServer } from '../../../lib/auth-server';

export default async function FulfillmentPage() {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) redirect('/login?returnUrl=/operations/fulfillment');
  if (!currentUser.roles?.some((role) => role.toLowerCase() === 'admin')) redirect('/shop');

  return (
    <FulfillmentQueue />
  );
}
