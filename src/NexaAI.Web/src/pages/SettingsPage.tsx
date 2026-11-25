import { useEffect } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Activity, Check, Cpu, Monitor, Moon, Save, Server, Sun } from 'lucide-react';
import { api } from '@services/api';
import { useTheme, type Theme } from '@hooks/useTheme';
import { useAuthStore } from '@store/authStore';
import { Badge } from '@components/ui/Badge';
import { Button } from '@components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@components/ui/Card';
import { Input } from '@components/ui/Input';
import { cn } from '@utils/cn';
import { formatDate, getInitials } from '@utils/format';

const profileSchema = z.object({
  fullName: z.string().min(2, 'Enter your name'),
  email: z.string().email('Enter a valid email address'),
});

type ProfileValues = z.infer<typeof profileSchema>;

const themeOptions: { value: Theme; label: string; description: string; icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', description: 'Bright and clear', icon: Sun },
  { value: 'dark', label: 'Dark', description: 'Easy on the eyes', icon: Moon },
  { value: 'system', label: 'System', description: 'Follow your device', icon: Monitor },
];

const featureFlags = [
  { name: 'Retrieval-augmented generation', enabled: true, note: 'Ground answers in indexed documents' },
  { name: 'Agent tool orchestration', enabled: true, note: 'Multi-step reasoning with connected tools' },
  { name: 'Human tool approvals', enabled: true, note: 'Require confirmation for sensitive actions' },
  { name: 'Response streaming', enabled: false, note: 'Token-by-token delivery in chat' },
];

export function SettingsPage() {
  const user = useAuthStore((state) => state.user);
  const updateUser = useAuthStore((state) => state.updateUser);
  const { theme, setTheme } = useTheme();

  const detailedHealth = useQuery({
    queryKey: ['health', 'detailed'],
    queryFn: () => api.health.detailed(),
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user?.fullName ?? user?.name ?? '', email: user?.email ?? '' },
  });

  useEffect(() => {
    reset({ fullName: user?.fullName ?? user?.name ?? '', email: user?.email ?? '' });
  }, [user, reset]);

  const saveProfile = useMutation({
    mutationFn: (values: ProfileValues) => api.auth.updateProfile(values),
    onSuccess: (updated) => {
      updateUser(updated);
      reset({ fullName: updated.fullName ?? updated.name ?? '', email: updated.email });
    },
  });

  const onSubmit = async (values: ProfileValues) => {
    await saveProfile.mutateAsync(values);
  };

  return (
    <div className="max-w-5xl space-y-6 animate-fade-in">
      <div>
        <h2 className="text-2xl font-semibold tracking-tight text-neutral-900 dark:text-white">Settings</h2>
        <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
          Manage your profile, appearance, and workspace information.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Profile</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Your account details.</p>
            </div>
          </CardHeader>
          <CardContent>
            <div className="mb-6 flex items-center gap-4 rounded-xl bg-gradient-to-r from-primary-50 to-indigo-50 p-4 dark:from-primary-900/20 dark:to-indigo-900/20">
              <span className="flex h-12 w-12 items-center justify-center rounded-full bg-gradient-to-br from-primary-500 to-indigo-600 text-sm font-semibold text-white">
                {getInitials(user?.fullName ?? user?.name, user?.email)}
              </span>
              <div>
                <p className="font-semibold text-neutral-800 dark:text-neutral-100">
                  {user?.fullName ?? user?.name ?? 'Your account'}
                </p>
                <p className="text-xs text-neutral-500 dark:text-neutral-400">{user?.email}</p>
              </div>
              {user?.role && <Badge variant="primary" className="ml-auto capitalize">{user.role}</Badge>}
            </div>

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
              <Input label="Full name" autoComplete="name" error={errors.fullName?.message} {...register('fullName')} />
              <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
              <div className="flex items-center gap-3 pt-2">
                <Button type="submit" isLoading={saveProfile.isPending} disabled={!isDirty} leftIcon={<Save className="h-4 w-4" />}>
                  Save changes
                </Button>
                {user?.createdAt && (
                  <span className="text-xs text-neutral-400">Joined {formatDate(user.createdAt)}</span>
                )}
              </div>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Appearance</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Choose how NexaAI looks to you.</p>
            </div>
          </CardHeader>
          <CardContent>
            <div role="radiogroup" aria-label="Theme" className="grid gap-3 sm:grid-cols-3">
              {themeOptions.map((option) => {
                const Icon = option.icon;
                const active = theme === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    role="radio"
                    aria-checked={active}
                    onClick={() => setTheme(option.value)}
                    className={cn(
                      'relative rounded-xl border p-4 text-left transition-all',
                      active
                        ? 'border-primary-500 bg-primary-50/70 shadow-sm dark:bg-primary-900/20'
                        : 'border-neutral-200 hover:border-primary-300 dark:border-neutral-700',
                    )}
                  >
                    {active && (
                      <span className="absolute right-3 top-3 flex h-5 w-5 items-center justify-center rounded-full bg-primary-600 text-white">
                        <Check className="h-3 w-3" />
                      </span>
                    )}
                    <Icon className="h-5 w-5 text-primary-600 dark:text-primary-400" />
                    <p className="mt-3 text-sm font-semibold text-neutral-800 dark:text-neutral-100">{option.label}</p>
                    <p className="mt-0.5 text-xs text-neutral-500 dark:text-neutral-400">{option.description}</p>
                  </button>
                );
              })}
            </div>
            <p className="mt-4 text-xs text-neutral-400">
              System mode follows your device preference automatically.
            </p>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Model &amp; services</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
              Read-only information reported by the API.
            </p>
          </div>
          <Server className="h-5 w-5 text-neutral-400" />
        </CardHeader>
        <CardContent>
          {detailedHealth.isLoading ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {[0, 1, 2].map((index) => (
                <div key={index} className="skeleton h-24 rounded-xl" />
              ))}
            </div>
          ) : detailedHealth.data?.checks?.length ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {detailedHealth.data.checks.map((check) => {
                const healthy = check.status.toLowerCase().includes('healthy');
                return (
                  <div key={check.name} className="rounded-xl border border-neutral-200 p-4 dark:border-neutral-700">
                    <div className="flex items-center justify-between gap-2">
                      <span className="flex items-center gap-2 text-sm font-medium capitalize text-neutral-800 dark:text-neutral-100">
                        <Cpu className="h-4 w-4 text-primary-600" /> {check.name}
                      </span>
                      <Badge variant={healthy ? 'success' : 'warning'} dot>{check.status}</Badge>
                    </div>
                    <p className="mt-2 text-xs text-neutral-500 dark:text-neutral-400">
                      {check.description ?? 'Dependency health check'}
                    </p>
                    {check.duration !== undefined && (
                      <p className="mt-1 text-[11px] text-neutral-400">{check.duration.toFixed(1)} ms</p>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="rounded-xl bg-neutral-50 p-4 text-sm text-neutral-500 dark:bg-neutral-800/60 dark:text-neutral-400">
              Detailed service information is not available right now.
            </p>
          )}
          <div className="mt-4 flex items-center gap-2 text-xs text-neutral-400">
            <Activity className="h-3.5 w-3.5" />
            Overall status: {detailedHealth.data?.status ?? (detailedHealth.isError ? 'Unavailable' : 'Checking…')}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Feature flags</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Capabilities enabled for this workspace.</p>
          </div>
        </CardHeader>
        <CardContent className="divide-y divide-neutral-100 dark:divide-neutral-800">
          {featureFlags.map((flag) => (
            <div key={flag.name} className="flex flex-col gap-2 py-3 first:pt-0 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-sm font-medium text-neutral-800 dark:text-neutral-100">{flag.name}</p>
                <p className="text-xs text-neutral-500 dark:text-neutral-400">{flag.note}</p>
              </div>
              <Badge variant={flag.enabled ? 'success' : 'neutral'} dot>
                {flag.enabled ? 'Enabled' : 'Planned'}
              </Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
