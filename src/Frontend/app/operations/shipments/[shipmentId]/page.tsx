import { redirect } from 'next/navigation';
import ShipmentDetails from '../../../../components/operations/ShipmentDetails';
import { getCurrentUserServer } from '../../../../lib/auth-server';

type ShipmentDetailsPageProps = { params: Promise<{ shipmentId: string }> };

export default async function ShipmentDetailsPage({ params }: ShipmentDetailsPageProps) {
  const currentUser = await getCurrentUserServer();
  const { shipmentId } = await params;
  if (!currentUser) redirect(`/login?returnUrl=/operations/shipments/${shipmentId}`);
  if (!currentUser.roles?.some((role) => role.toLowerCase() === 'admin')) redirect('/shop');

  return <ShipmentDetails shipmentId={shipmentId} />;
}