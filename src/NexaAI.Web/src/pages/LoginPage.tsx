import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowRight, BrainCircuit, Eye, EyeOff, LockKeyhole, Mail, ShieldCheck, Sparkles } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { useAuth } from '@hooks/useAuth';
import { Button } from '@components/ui/Button';
import { Input } from '@components/ui/Input';

const loginSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

type LoginFormValues = z.infer<typeof loginSchema>;

function BrandPanel() {
  return (
    <div className="relative hidden overflow-hidden bg-gradient-to-br from-primary-700 via-primary-800 to-slate-900 p-12 text-white lg:flex lg:w-1/2 lg:flex-col lg:justify-between">
      <div className="pointer-events-none absolute -right-24 -top-24 h-96 w-96 rounded-full bg-primary-400/20 blur-3xl" />
      <div className="pointer-events-none absolute -bottom-32 -left-24 h-[28rem] w-[28rem] rounded-full bg-indigo-500/20 blur-3xl" />
      <div className="relative flex items-center gap-3">
        <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-white/15 backdrop-blur">
          <Sparkles className="h-6 w-6" />
        </span>
        <div>
          <p className="text-xl font-bold tracking-tight">NexaAI</p>
          <p className="text-xs uppercase tracking-[0.2em] text-primary-100">Enterprise Assistant</p>
        </div>
      </div>

      <div className="relative max-w-lg">
        <h2 className="text-balance text-4xl font-semibold leading-tight tracking-tight">
          Your organization&apos;s knowledge, ready to answer.
        </h2>
        <p className="mt-5 text-base leading-relaxed text-primary-100/90">
          Ground every answer in your documents, orchestrate capable agents, and measure quality — all from one workspace.
        </p>
        <div className="mt-10 space-y-4">
          {[
            { icon: BrainCircuit, title: 'Grounded RAG answers', copy: 'Citations back every response' },
            { icon: ShieldCheck, title: 'Governed by design', copy: 'Approvals and access controls built in' },
          ].map((feature) => (
            <div key={feature.title} className="flex items-start gap-4 rounded-2xl border border-white/10 bg-white/5 p-4 backdrop-blur">
              <feature.icon className="mt-0.5 h-5 w-5 shrink-0 text-primary-200" />
              <div>
                <p className="text-sm font-semibold">{feature.title}</p>
                <p className="mt-0.5 text-xs text-primary-100/80">{feature.copy}</p>
              </div>
            </div>
          ))}
        </div>
      </div>

      <p className="relative text-xs text-primary-100/60">© {new Date().getFullYear()} NexaAI. Built for secure teams.</p>
    </div>
  );
}

export function LoginPage() {
  const navigate = useNavigate();
  const { signIn, isLoading, error } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async (values: LoginFormValues) => {
    await signIn(values);
    navigate('/', { replace: true });
  };

  return (
    <div className="flex min-h-screen bg-white dark:bg-neutral-950">
      <BrandPanel />
      <div className="flex w-full items-center justify-center bg-gradient-to-b from-neutral-50 to-white px-6 py-12 dark:from-neutral-950 dark:to-neutral-900 lg:w-1/2">
        <div className="w-full max-w-md">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 text-white">
              <Sparkles className="h-5 w-5" />
            </span>
            <span className="text-lg font-bold">NexaAI</span>
          </div>

          <h1 className="text-3xl font-semibold tracking-tight text-neutral-900 dark:text-white">
            Welcome back
          </h1>
          <p className="mt-2 text-sm text-neutral-500 dark:text-neutral-400">
            Sign in to continue to your AI workspace.
          </p>

          {error && (
            <div role="alert" className="mt-6 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
              {error.message}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5" noValidate>
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
              autoComplete="current-password"
              placeholder="Enter your password"
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
            <Button type="submit" fullWidth size="lg" isLoading={isSubmitting || isLoading} rightIcon={<ArrowRight className="h-4 w-4" />}>
              Sign in
            </Button>
          </form>

          <p className="mt-8 text-center text-sm text-neutral-500 dark:text-neutral-400">
            New to NexaAI?{' '}
            <Link to="/register" className="font-semibold text-primary-700 hover:underline dark:text-primary-300">
              Create an account
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
