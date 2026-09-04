'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { changePassword, logout, updateProfile } from '../../lib/api/auth';
import { useCurrentUser } from '../../lib/queries/auth';
import { passwordChangeSchema, profileSchema, type PasswordChangeFormValues, type ProfileFormValues } from '../../lib/validation/account';

export default function AccountView() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const userQuery = useCurrentUser();
  const [passwordFormOpen, setPasswordFormOpen] = useState(false);
  const [passwordChanged, setPasswordChanged] = useState(false);
  const [saved, setSaved] = useState(false);
  const {
    register: registerProfile,
    handleSubmit: handleProfileSubmit,
    reset: resetProfile,
    formState: { errors: profileErrors },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });
  const {
    register: registerPassword,
    handleSubmit: handlePasswordSubmit,
    reset: resetPassword,
    formState: { errors: passwordErrors },
  } = useForm<PasswordChangeFormValues>({
    resolver: zodResolver(passwordChangeSchema),
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });

  useEffect(() => {
    if (!userQuery.isPending && !userQuery.isFetching && userQuery.data === null) {
      router.replace('/login?returnUrl=/account');
    }
  }, [router, userQuery.data, userQuery.isFetching, userQuery.isPending]);

  useEffect(() => {
    if (userQuery.data) {
      resetProfile({ firstName: userQuery.data.firstName, lastName: userQuery.data.lastName });
    }
  }, [resetProfile, userQuery.data]);

  const profileMutation = useMutation({
    mutationFn: (profile: ProfileFormValues) => updateProfile(profile),
    onSuccess: async (user) => {
      setSaved(true);
      resetProfile({ firstName: user.firstName, lastName: user.lastName });
      await queryClient.setQueryData(['current-user'], user);
    },
  });

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(['current-user'], null);
      router.replace('/login');
    },
  });

  const passwordMutation = useMutation({
    mutationFn: ({ currentPassword, newPassword }: PasswordChangeFormValues) => changePassword(currentPassword, newPassword),
    onSuccess: () => {
      resetPassword();
      setPasswordChanged(true);
      setPasswordFormOpen(false);
    },
  });

  if (userQuery.isPending || !userQuery.data) {
    return <main className="mx-auto flex min-h-[calc(100vh-64px)] max-w-[720px] items-center justify-center px-lg py-3xl text-secondary">Loading account...</main>;
  }

  return (
    <main className="mx-auto min-h-[calc(100vh-64px)] w-full max-w-[1280px] px-lg py-3xl md:px-xl">
      <div className="mx-auto w-full max-w-[720px]">
        <h1 className="mb-xl font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">Account</h1>
        <section className="mb-xl rounded-lg border border-border-standard bg-surface-card p-xl shadow-[0_2px_4px_rgba(0,0,0,0.02)]">
          <h2 className="mb-lg border-b border-border-standard pb-md font-product-title text-product-title text-on-surface">Profile</h2>
          <form className="space-y-lg" onSubmit={handleProfileSubmit((values) => { setSaved(false); profileMutation.mutate(values); })} noValidate>
            <div className="grid grid-cols-1 gap-lg md:grid-cols-2">
              <label className="font-label-caps text-label-caps text-on-surface-variant" htmlFor="firstName">
                First name
                <input className="mt-sm h-control w-full rounded-lg border border-border-standard bg-surface-card px-md font-body-md text-body-md text-on-surface" id="firstName" aria-invalid={profileErrors.firstName ? true : undefined} aria-describedby={profileErrors.firstName ? 'firstName-error' : undefined} {...registerProfile('firstName')} />
                {profileErrors.firstName ? <span className="mt-xs block font-status-pill text-status-pill text-status-error" id="firstName-error" role="alert">{profileErrors.firstName.message}</span> : null}
              </label>
              <label className="font-label-caps text-label-caps text-on-surface-variant" htmlFor="lastName">
                Last name
                <input className="mt-sm h-control w-full rounded-lg border border-border-standard bg-surface-card px-md font-body-md text-body-md text-on-surface" id="lastName" aria-invalid={profileErrors.lastName ? true : undefined} aria-describedby={profileErrors.lastName ? 'lastName-error' : undefined} {...registerProfile('lastName')} />
                {profileErrors.lastName ? <span className="mt-xs block font-status-pill text-status-pill text-status-error" id="lastName-error" role="alert">{profileErrors.lastName.message}</span> : null}
              </label>
            </div>
            <label className="block font-label-caps text-label-caps text-on-surface-variant" htmlFor="email">
              Email
              <input className="mt-sm h-control w-full cursor-not-allowed rounded-lg border border-border-standard bg-surface-container-low px-md font-body-md text-body-md text-on-surface-variant" id="email" value={userQuery.data.email} readOnly />
            </label>
            <div className="flex items-center gap-lg pt-md">
              <button className="h-control rounded-button bg-primary px-lg font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container disabled:opacity-60" type="submit" disabled={profileMutation.isPending}>
                {profileMutation.isPending ? 'Saving...' : 'Save changes'}
              </button>
              {saved ? <span className="font-status-pill text-status-pill text-status-success" role="status">Changes saved</span> : null}
              {profileMutation.isError ? <span className="font-status-pill text-status-pill text-status-error" role="alert">Unable to save changes.</span> : null}
            </div>
          </form>
        </section>
        <section className="rounded-lg border border-border-standard bg-surface-card p-xl shadow-[0_2px_4px_rgba(0,0,0,0.02)]">
          <h2 className="mb-lg border-b border-border-standard pb-md font-product-title text-product-title text-on-surface">Security</h2>
          <div className="space-y-xl">
            <div className="flex flex-col justify-between gap-lg border-b border-border-subtle pb-xl md:flex-row md:items-center">
              <p className="max-w-md font-body-md text-body-md text-on-surface-variant">Update your password to keep your account secure.</p>
              <button className="h-control whitespace-nowrap rounded-button border border-border-standard bg-surface-card px-lg font-label-caps text-label-caps text-on-surface hover:bg-surface-container-high" type="button" onClick={() => { setPasswordFormOpen((open) => !open); setPasswordChanged(false); }}>
                {passwordFormOpen ? 'Cancel' : 'Change password'}
              </button>
            </div>
            {passwordFormOpen ? <form className="grid gap-lg border-b border-border-subtle pb-xl md:grid-cols-2" onSubmit={handlePasswordSubmit((values) => { setPasswordChanged(false); passwordMutation.mutate(values); })} noValidate>
              <label className="font-label-caps text-label-caps text-on-surface-variant" htmlFor="currentPassword">Current password<input className="mt-sm h-control w-full rounded-lg border border-border-standard px-md font-body-md text-body-md text-on-surface" id="currentPassword" type="password" autoComplete="current-password" aria-invalid={passwordErrors.currentPassword ? true : undefined} aria-describedby={passwordErrors.currentPassword ? 'currentPassword-error' : undefined} {...registerPassword('currentPassword')} />{passwordErrors.currentPassword ? <span className="mt-xs block font-status-pill text-status-pill text-status-error" id="currentPassword-error" role="alert">{passwordErrors.currentPassword.message}</span> : null}</label>
              <label className="font-label-caps text-label-caps text-on-surface-variant" htmlFor="newPassword">New password<input className="mt-sm h-control w-full rounded-lg border border-border-standard px-md font-body-md text-body-md text-on-surface" id="newPassword" type="password" autoComplete="new-password" aria-invalid={passwordErrors.newPassword ? true : undefined} aria-describedby={passwordErrors.newPassword ? 'newPassword-error' : undefined} {...registerPassword('newPassword')} />{passwordErrors.newPassword ? <span className="mt-xs block font-status-pill text-status-pill text-status-error" id="newPassword-error" role="alert">{passwordErrors.newPassword.message}</span> : null}</label>
              <div className="flex items-center gap-lg md:col-span-2"><button className="h-control rounded-button bg-primary px-lg font-label-caps text-label-caps uppercase text-on-primary disabled:opacity-60" type="submit" disabled={passwordMutation.isPending}>{passwordMutation.isPending ? 'Updating...' : 'Update password'}</button>{passwordChanged ? <span className="font-status-pill text-status-pill text-status-success" role="status">Password updated</span> : null}{passwordMutation.isError ? <span className="font-status-pill text-status-pill text-status-error" role="alert">Unable to update password.</span> : null}</div>
            </form> : null}
            <div className="flex flex-col justify-between gap-lg md:flex-row md:items-center">
              <p className="font-body-md text-body-md text-on-surface-variant">Sign out of your account on this device.</p>
              <button className="h-control whitespace-nowrap rounded-button border border-status-error/30 bg-surface-card px-lg font-label-caps text-label-caps text-status-error hover:bg-red-50 disabled:opacity-60" type="button" onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
                {logoutMutation.isPending ? 'Signing out...' : 'Logout'}
              </button>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}