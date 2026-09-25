import { notFound, redirect } from 'next/navigation';
import FulfillmentOrderDetails from '../../../../components/operations/FulfillmentOrderDetails';
import { getCurrentUserServer } from '../../../../lib/auth-server';
import { getFulfillmentOrderById } from '../../../../lib/server/orders';

type FulfillmentOrderDetailsPageProps = { params: Promise<{ orderId: string }> };

export default async function FulfillmentOrderDetailsPage({ params }: FulfillmentOrderDetailsPageProps) {
  const currentUser = await getCurrentUserServer();
  const { orderId } = await params;
  if (!currentUser) redirect(`/login?returnUrl=/operations/fulfillment/${orderId}`);
  if (!currentUser.roles?.some((role) => role.toLowerCase() === 'admin')) redirect('/shop');

  const order = await getFulfillmentOrderById(orderId);
  if (!order) notFound();

  return <FulfillmentOrderDetails order={order} />;
}