import { redirect } from 'next/navigation';
import ShipmentQueue from '../../../components/operations/ShipmentQueue';
import { getCurrentUserServer } from '../../../lib/auth-server';

export default async function ShipmentsPage() {
  const currentUser = await getCurrentUserServer();
  if (!currentUser) redirect('/login?returnUrl=/operations/shipments');
  if (!currentUser.roles?.some((role) => role.toLowerCase() === 'admin')) redirect('/shop');

  return <ShipmentQueue />;
}