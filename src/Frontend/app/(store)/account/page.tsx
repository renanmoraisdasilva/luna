import AccountView from '../../../components/account/AccountView';
import { getCurrentUserServer } from '../../../lib/auth-server';
import { redirect } from 'next/navigation';

export default async function AccountPage() {
  const user = await getCurrentUserServer();
  if (!user) {
    redirect('/login?returnUrl=/account');
  }

  return <AccountView initialUser={user} />;
}