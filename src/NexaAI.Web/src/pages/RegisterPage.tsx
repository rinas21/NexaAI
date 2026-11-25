import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowRight, Eye, EyeOff, LockKeyhole, Mail, Sparkles, UserRound } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { useAuth } from '@hooks/useAuth';
import { Button } from '@components/ui/Button';
import { Input } from '@components/ui/Input';

const registerSchema = z
  .object({
    fullName: z.string().min(2, 'Enter your full name'),
    email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
    password: z.string().min(8, 'Use at least 8 characters'),
    confirmPassword: z.string().min(1, 'Confirm your password'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

type RegisterFormValues = z.infer<typeof registerSchema>;

export function RegisterPage() {
  const navigate = useNavigate();
  const { signUp, isLoading, error } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', password: '', confirmPassword: '' },
  });

  const onSubmit = async ({ confirmPassword: _confirmPassword, ...values }: RegisterFormValues) => {
    void _confirmPassword;
    await signUp(values);
    navigate('/', { replace: true });
  };

  return (
    <div className="flex min-h-screen bg-neutral-50 dark:bg-neutral-950">
      <div className="flex w-full items-center justify-center px-6 py-12">
        <div className="w-full max-w-md">
          <div className="mb-8 flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 text-white">
              <Sparkles className="h-5 w-5" />
            </span>
            <div>
              <p className="text-lg font-bold leading-tight text-neutral-900 dark:text-white">NexaAI</p>
              <p className="text-[10px] uppercase tracking-[0.18em] text-neutral-400">Enterprise Assistant</p>
            </div>
          </div>

          <h1 className="text-3xl font-semibold tracking-tight text-neutral-900 dark:text-white">
            Create your account
          </h1>
          <p className="mt-2 text-sm text-neutral-500 dark:text-neutral-400">
            Join your team&apos;s secure AI workspace in a minute.
          </p>

          {error && (
            <div role="alert" className="mt-6 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
              {error.message}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5" noValidate>
            <Input
              label="Full name"
              autoComplete="name"
              placeholder="Ada Lovelace"
              leftIcon={<UserRound className="h-4 w-4" />}
              error={errors.fullName?.message}
              {...register('fullName')}
            />
            <Input
              label="Work email"
              type="email"
              autoComplete="email"
              placeholder="you@company.com"
              leftIcon={<Mail className="h-4 w-4" />}
              error={errors.email?.message}
              {...register('email')}
            />
            <Input
              label="Password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              placeholder="At least 8 characters"
              leftIcon={<LockKeyhole className="h-4 w-4" />}
              error={errors.password?.message}
              rightSlot={
                <button
                  type="button"
                  onClick={() => setShowPassword((visible) => !visible)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  className="btn-icon btn-ghost text-neutral-400"
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              }
              {...register('password')}
            />
            <Input
              label="Confirm password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              placeholder="Re-enter your password"
              leftIcon={<LockKeyhole className="h-4 w-4" />}
              error={errors.confirmPassword?.message}
              {...register('confirmPassword')}
            />
            <Button type="submit" fullWidth size="lg" isLoading={isSubmitting || isLoading} rightIcon={<ArrowRight className="h-4 w-4" />}>
              Create account
            </Button>
          </form>

          <p className="mt-8 text-center text-sm text-neutral-500 dark:text-neutral-400">
            Already have an account?{' '}
            <Link to="/login" className="font-semibold text-primary-700 hover:underline dark:text-primary-300">
              Sign in
            </Link>
          </p>
        </div>
      </div>

      <div className="relative hidden w-1/2 overflow-hidden bg-gradient-to-br from-slate-900 via-primary-900 to-primary-700 lg:flex lg:items-center lg:justify-center">
        <div className="pointer-events-none absolute -left-20 top-10 h-96 w-96 rounded-full bg-primary-400/20 blur-3xl" />
        <div className="relative max-w-md px-12 text-white">
          <Sparkles className="h-10 w-10 text-primary-200" />
          <blockquote className="mt-8 text-2xl font-medium leading-relaxed">
            &ldquo;Answers you can trust, built on the knowledge your team already owns.&rdquo;
          </blockquote>
          <p className="mt-6 text-sm text-primary-100/80">
            Documents, agents, and evaluation — connected in one thoughtful workspace.
          </p>
        </div>
      </div>
    </div>
  );
}
