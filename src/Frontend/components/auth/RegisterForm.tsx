'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import axios from 'axios';
import { registerAccount } from '../../lib/api/identity';
import { registerSchema, type RegisterFormValues } from '../../lib/validation/auth';

export default function RegisterForm() {
  const [showPassword, setShowPassword] = useState(false);
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });
  const registerMutation = useMutation({
    mutationFn: registerAccount,
    onSuccess: (_, values) => setRegisteredEmail(values.email),
  });

  function handleRegister(values: RegisterFormValues) {
    registerMutation.mutate({
      email: values.email,
      password: values.password,
      firstName: values.firstName,
      lastName: values.lastName,
    });
  }

  if (registeredEmail) {
    return (
      <div className="relative z-10 w-full max-w-[440px] rounded bg-surface-card p-lg text-center shadow-sm sm:p-xl">
        <div className="mx-auto mb-lg flex h-16 w-16 items-center justify-center rounded-full border border-secondary-fixed bg-secondary-fixed/30 text-primary">✓</div>
        <h2 className="font-headline-lg text-headline-lg text-primary">Check your email</h2>
        <p className="mt-md font-body-md text-body-md text-secondary">We&apos;ve sent a verification link to {registeredEmail}.</p>
        <p className="mt-sm font-status-pill text-status-pill text-secondary">Please click the link to activate your Luna account.</p>
        <Link className="mt-xl flex h-control items-center justify-center rounded bg-primary font-product-title text-product-title text-on-primary transition-opacity hover:opacity-95" href="/login">Back to sign in</Link>
      </div>
    );
  }

  const apiError = registerMutation.isError
    ? axios.isAxiosError(registerMutation.error) && registerMutation.error.response?.status === 400
      ? 'This account could not be created. Check your details and try again.'
      : 'Unable to create your account right now. Please try again.'
    : null;

  return (
    <div className="relative z-10 w-full max-w-[440px] rounded bg-surface-card p-lg shadow-sm sm:p-xl">
      <form className="flex flex-col gap-xl" onSubmit={handleSubmit(handleRegister)} noValidate>
        <div className="flex gap-sm text-left">
          <div className="flex min-w-0 flex-1 flex-col gap-xs">
            <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="firstName">First name</label>
            <input className="h-control w-full rounded border border-transparent bg-surface-base px-md font-body-md text-body-md text-on-surface outline-none focus:border-border-standard focus:bg-surface-card" id="firstName" placeholder="Jane" autoComplete="given-name" aria-invalid={errors.firstName ? true : undefined} {...register('firstName')} />
            {errors.firstName ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.firstName.message}</p> : null}
          </div>
          <div className="flex min-w-0 flex-1 flex-col gap-xs">
            <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="lastName">Last name</label>
            <input className="h-control w-full rounded border border-transparent bg-surface-base px-md font-body-md text-body-md text-on-surface outline-none focus:border-border-standard focus:bg-surface-card" id="lastName" placeholder="Doe" autoComplete="family-name" aria-invalid={errors.lastName ? true : undefined} {...register('lastName')} />
            {errors.lastName ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.lastName.message}</p> : null}
          </div>
        </div>
        <div className="flex flex-col gap-xs text-left">
          <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="email">Email address</label>
          <input className="h-control rounded border border-transparent bg-surface-base px-md font-body-md text-body-md text-on-surface outline-none focus:border-border-standard focus:bg-surface-card" id="email" placeholder="jane.doe@example.com" type="email" autoComplete="email" aria-invalid={errors.email ? true : undefined} {...register('email')} />
          {errors.email ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.email.message}</p> : null}
        </div>
        <div className="flex flex-col gap-xs text-left">
          <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="password">Password</label>
          <div className="relative flex items-center">
            <input className="h-control w-full rounded border border-transparent bg-surface-base px-md pr-16 font-body-md text-body-md text-on-surface outline-none focus:border-border-standard focus:bg-surface-card" id="password" placeholder="••••••••" type={showPassword ? 'text' : 'password'} autoComplete="new-password" aria-invalid={errors.password ? true : undefined} {...register('password')} />
            <button className="absolute right-0 top-0 flex h-full items-center px-md font-status-pill text-status-pill text-secondary" type="button" onClick={() => setShowPassword((value) => !value)} aria-label={showPassword ? 'Hide password' : 'Show password'}>{showPassword ? 'Hide' : 'Show'}</button>
          </div>
          <p className="font-status-pill text-status-pill text-secondary">8+ characters, including uppercase, lowercase, and a symbol</p>
          {errors.password ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.password.message}</p> : null}
        </div>
        <div className="flex flex-col gap-xs text-left">
          <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="confirmPassword">Confirm password</label>
          <input className="h-control rounded border border-transparent bg-surface-base px-md font-body-md text-body-md text-on-surface outline-none focus:border-border-standard focus:bg-surface-card" id="confirmPassword" placeholder="••••••••" type="password" autoComplete="new-password" aria-invalid={errors.confirmPassword ? true : undefined} {...register('confirmPassword')} />
          {errors.confirmPassword ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.confirmPassword.message}</p> : null}
        </div>
        <div className="flex flex-col gap-xs pt-xs text-left">
          <label className="flex items-start gap-sm font-status-pill text-status-pill text-on-surface-variant">
            <input className="mt-0.5 h-4 w-4 accent-primary" type="checkbox" {...register('terms')} />
            <span>I agree to the <a className="font-medium text-primary hover:underline" href="#terms">Terms of Service</a> and <a className="font-medium text-primary hover:underline" href="#privacy">Privacy Policy</a></span>
          </label>
          {errors.terms ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{errors.terms.message}</p> : null}
        </div>
        {apiError ? <p className="font-status-pill text-status-pill font-bold text-status-error" role="alert">{apiError}</p> : null}
        <button className="flex h-control w-full items-center justify-center rounded bg-primary font-product-title text-product-title text-on-primary transition-opacity hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60" type="submit" disabled={registerMutation.isPending}>{registerMutation.isPending ? 'Creating account...' : 'Create account'}</button>
      </form>
      <div className="-mx-lg -mb-lg mt-xl bg-surface-base p-lg text-center sm:-mx-xl sm:-mb-xl">
        <p className="font-body-md text-[14px] text-secondary">Already have an account? <Link className="font-product-title text-[14px] font-semibold text-primary hover:underline" href="/login">Sign in</Link></p>
      </div>
    </div>
  );
}
