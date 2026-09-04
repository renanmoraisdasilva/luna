'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import axios from 'axios';
import { login } from '../../lib/api/identity';
import { loginSchema, type LoginFormValues } from '../../lib/validation/auth';

export default function LoginForm() {
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });
  const loginMutation = useMutation({
    mutationFn: login,
    onSuccess: () => router.push('/shop'),
  });

  function handleLogin(values: LoginFormValues) {
    loginMutation.mutate(values);
  }

  const apiError = loginMutation.isError
    ? axios.isAxiosError(loginMutation.error) && loginMutation.error.response?.status === 401
      ? 'Invalid email or password.'
      : 'Unable to sign in right now. Please try again.'
    : null;

  return (
    <div className="relative z-10 w-full max-w-[440px] rounded bg-surface-card p-lg shadow-sm sm:p-xl">
      <form className="flex flex-col gap-xl" onSubmit={handleSubmit(handleLogin)} noValidate>
        <div className="flex flex-col gap-xs text-left">
          <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="email">Email address</label>
          <input
            className="h-control rounded border border-transparent bg-surface-base px-md font-body-md text-body-md text-on-surface outline-none transition-all focus:border-border-standard focus:bg-surface-card"
            id="email"
            placeholder="jane.doe@example.com"
            type="email"
            autoComplete="email"
            aria-invalid={errors.email ? true : undefined}
            aria-describedby={errors.email ? 'email-error' : undefined}
            {...register('email')}
          />
          {errors.email ? <p className="font-status-pill text-status-pill text-status-error" id="email-error" role="alert">{errors.email.message}</p> : null}
        </div>
        <div className="flex flex-col gap-xs text-left">
          <div className="flex items-center justify-between">
            <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor="password">Password</label>
            <a className="font-status-pill text-status-pill text-secondary transition-colors hover:text-primary" href="#forgot">Forgot password?</a>
          </div>
          <div className="relative flex items-center">
            <input
              className="h-control w-full rounded border border-transparent bg-surface-base px-md pr-16 font-body-md text-body-md text-on-surface outline-none transition-all focus:border-border-standard focus:bg-surface-card"
              id="password"
              placeholder="••••••••"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              aria-invalid={errors.password ? true : undefined}
              aria-describedby={errors.password ? 'password-error' : undefined}
              {...register('password')}
            />
            <button className="absolute right-0 top-0 flex h-full items-center px-md font-status-pill text-status-pill text-secondary transition-colors hover:text-on-surface" type="button" onClick={() => setShowPassword((value) => !value)} aria-label={showPassword ? 'Hide password' : 'Show password'}>
              {showPassword ? 'Hide' : 'Show'}
            </button>
          </div>
          {errors.password ? <p className="font-status-pill text-status-pill text-status-error" id="password-error" role="alert">{errors.password.message}</p> : null}
        </div>
        {apiError ? <p className="font-status-pill text-status-pill font-bold text-status-error" role="alert">{apiError}</p> : null}
        <label className="flex cursor-pointer select-none items-center gap-sm font-status-pill text-status-pill text-on-surface-variant">
          <input className="h-4 w-4 accent-primary" type="checkbox" name="remember" />
          Remember this device
        </label>
        <button className="flex h-control w-full items-center justify-center rounded bg-primary font-product-title text-product-title text-on-primary transition-opacity hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60" type="submit" disabled={loginMutation.isPending}>
          {loginMutation.isPending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
      <div className="-mx-lg -mb-lg mt-xl bg-surface-base p-lg text-center sm:-mx-xl sm:-mb-xl">
        <p className="font-body-md text-[14px] text-secondary">Don&apos;t have an account? <Link className="font-product-title text-[14px] font-semibold text-primary hover:underline" href="/register">Create an account</Link></p>
      </div>
    </div>
  );
}
