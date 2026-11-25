import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  Activity,
  ArrowUpRight,
  Bot,
  Clock3,
  FileText,
  MessageSquare,
  Plus,
  Server,
  Upload,
} from 'lucide-react';
import { api } from '@services/api';
import { useConversations } from '@hooks/useChat';
import { useDocuments } from '@hooks/useDocuments';
import { Badge } from '@components/ui/Badge';
import { Button } from '@components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle, StatCard } from '@components/ui/Card';
import { EmptyState } from '@components/ui/EmptyState';
import { Skeleton } from '@components/ui/Skeleton';
import { formatRelative } from '@utils/format';

export function DashboardPage() {
  const documents = useDocuments();
  const conversations = useConversations();
  const agents = useQuery({ queryKey: ['agents', 'executions'], queryFn: () => api.agents.list() });
  const health = useQuery({ queryKey: ['health'], queryFn: () => api.health.get() });

  const documentCount = documents.data?.length ?? 0;
  const conversationCount = conversations.data?.length ?? 0;
  const agentCount = agents.data?.length ?? 0;
  const runs = agents.data ?? [];
  const avgLatency = runs.length
    ? Math.round(
        runs.reduce((sum, run) => sum + (run.executionTimeMs ?? run.durationMs ?? 0), 0) / runs.length,
      )
    : 0;

  const activity = [
    ...(documents.data ?? []).map((document) => ({
      id: `doc-${document.id}`,
      label: `Uploaded “${document.title}”`,
      detail: document.status,
      date: document.createdAt,
      icon: FileText,
      to: '/documents',
    })),
    ...(conversations.data ?? []).map((conversation) => ({
      id: `chat-${conversation.id}`,
      label: conversation.title || 'New conversation',
      detail: 'Conversation',
      date: conversation.updatedAt,
      icon: MessageSquare,
      to: '/chat',
    })),
    ...runs.slice(0, 5).map((run) => ({
      id: `agent-${run.id}`,
      label: run.agentName ?? 'Agent execution',
      detail: run.status ?? 'Completed',
      date: run.startedAt ?? run.createdAt ?? '',
      icon: Bot,
      to: '/agents',
    })),
  ]
    .filter((item) => item.date)
    .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
    .slice(0, 6);

  const healthChecks = health.data?.checks ?? [];
  const isHealthy = health.data?.status?.toLowerCase().includes('healthy') ?? false;

  return (
    <div className="space-y-8 animate-fade-in">
      <section className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-primary-600 via-primary-700 to-slate-900 px-6 py-8 text-white shadow-soft sm:px-9 sm:py-10">
        <div className="pointer-events-none absolute -right-16 -top-24 h-72 w-72 rounded-full bg-white/10 blur-2xl" />
        <div className="relative flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
          <div className="max-w-2xl">
            <span className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-medium backdrop-blur">
              <Activity className="h-3.5 w-3.5" /> Workspace overview
            </span>
            <h2 className="mt-4 text-balance text-3xl font-semibold tracking-tight sm:text-4xl">
              Good to see you. Let&apos;s put your knowledge to work.
            </h2>
            <p className="mt-3 text-sm leading-relaxed text-primary-100/90">
              Ask grounded questions, manage documents, and keep an eye on your AI operations from one place.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link to="/chat">
              <Button variant="secondary" leftIcon={<MessageSquare className="h-4 w-4" />}>
                New chat
              </Button>
            </Link>
            <Link to="/documents">
              <Button variant="outline" className="border-white/30 bg-white/10 text-white hover:bg-white/20" leftIcon={<Upload className="h-4 w-4" />}>
                Upload document
              </Button>
            </Link>
          </div>
        </div>
      </section>

      <section aria-label="Key metrics" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Documents" value={documentCount} icon={<FileText className="h-5 w-5" />} helper="Files in your knowledge base" tone="primary" />
        <StatCard label="Conversations" value={conversationCount} icon={<MessageSquare className="h-5 w-5" />} helper="Chats with your assistant" tone="purple" />
        <StatCard label="Agent runs" value={agentCount} icon={<Bot className="h-5 w-5" />} helper="Executions recorded" tone="success" />
        <StatCard label="Avg. latency" value={avgLatency ? `${avgLatency} ms` : '—'} icon={<Clock3 className="h-5 w-5" />} helper="Across recent agent runs" tone="warning" />
      </section>

      <div className="grid gap-6 xl:grid-cols-3">
        <Card className="xl:col-span-2">
          <CardHeader>
            <div>
              <CardTitle>Recent activity</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">The latest changes across your workspace.</p>
            </div>
            <Link to="/documents" className="inline-flex items-center gap-1 text-xs font-semibold text-primary-700 hover:underline dark:text-primary-300">
              View all <ArrowUpRight className="h-3.5 w-3.5" />
            </Link>
          </CardHeader>
          <CardContent className="space-y-1">
            {documents.isLoading || conversations.isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
              </div>
            ) : activity.length === 0 ? (
              <EmptyState
                title="No activity yet"
                description="Upload a document or start a conversation to see activity here."
                action={
                  <Link to="/chat">
                    <Button size="sm" leftIcon={<Plus className="h-4 w-4" />}>Start chatting</Button>
                  </Link>
                }
              />
            ) : (
              activity.map((item) => (
                <Link
                  key={item.id}
                  to={item.to}
                  className="flex items-center gap-3 rounded-xl px-3 py-3 transition-colors hover:bg-neutral-50 dark:hover:bg-neutral-800/60"
                >
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-300">
                    <item.icon className="h-4 w-4" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-neutral-800 dark:text-neutral-100">{item.label}</span>
                    <span className="block text-xs capitalize text-neutral-500 dark:text-neutral-400">{item.detail}</span>
                  </span>
                  <span className="shrink-0 text-xs text-neutral-400">{formatRelative(item.date)}</span>
                </Link>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>System health</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Live service status.</p>
            </div>
            <Badge variant={isHealthy ? 'success' : health.isError ? 'danger' : 'warning'} dot>
              {health.isError ? 'Unavailable' : isHealthy ? 'Healthy' : 'Checking'}
            </Badge>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center gap-3 rounded-xl bg-neutral-50 p-3 dark:bg-neutral-800/50">
              <Server className="h-5 w-5 text-primary-600 dark:text-primary-400" />
              <div>
                <p className="text-sm font-medium text-neutral-800 dark:text-neutral-100">API service</p>
                <p className="text-xs text-neutral-500 dark:text-neutral-400">
                  {health.isLoading ? 'Checking…' : health.isError ? 'Not reachable' : health.data?.status ?? 'Unknown'}
                </p>
              </div>
            </div>
            {healthChecks.length > 0 ? (
              healthChecks.map((check) => (
                <div key={check.name} className="flex items-center justify-between gap-3 border-t border-neutral-100 pt-3 dark:border-neutral-800">
                  <span className="text-sm capitalize text-neutral-700 dark:text-neutral-200">{check.name}</span>
                  <Badge variant={check.status.toLowerCase().includes('healthy') ? 'success' : 'warning'}>
                    {check.status}
                  </Badge>
                </div>
              ))
            ) : (
              <p className="pt-1 text-xs text-neutral-500 dark:text-neutral-400">
                Detailed checks are available on the settings page.
              </p>
            )}
            <Link to="/settings" className="inline-flex items-center gap-1 pt-2 text-xs font-semibold text-primary-700 hover:underline dark:text-primary-300">
              Service details <ArrowUpRight className="h-3.5 w-3.5" />
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
