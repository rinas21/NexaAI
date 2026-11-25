import { useQuery } from '@tanstack/react-query';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Activity, BarChart3, Gauge, Play, Timer, TrendingUp } from 'lucide-react';
import { api } from '@services/api';
import { useEvaluationRuns, useEvaluationSummary, useStartEvaluation } from '@hooks/useEvaluation';
import { Badge, type BadgeVariant } from '@components/ui/Badge';
import { Button } from '@components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle, StatCard } from '@components/ui/Card';
import { Table, type Column } from '@components/ui/Table';
import { formatDate, formatDuration, formatPercent } from '@utils/format';
import type { EvaluationRun, EvaluationTrendPoint } from '@/types';

function numeric(value: number | undefined): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

const axisStyle = { fill: '#a3a3a3', fontSize: 11 } as const;

export function EvaluationPage() {
  const summary = useEvaluationSummary();
  const runs = useEvaluationRuns();
  const start = useStartEvaluation();
  const health = useQuery({ queryKey: ['health', 'detailed'], queryFn: () => api.health.detailed() });

  const averageMetrics = summary.data?.averageMetrics ?? {};
  const recall = numeric(averageMetrics.recallAtK ?? averageMetrics.averageRecall);
  const mrr = numeric(averageMetrics.mrr ?? averageMetrics.averageMrr);
  const faithfulness = numeric(averageMetrics.faithfulness ?? averageMetrics.averageFaithfulness);
  const latency = numeric(averageMetrics.latencyMs ?? averageMetrics.averageLatencyMs);
  const p95 = numeric(averageMetrics.p95LatencyMs);

  const runList = runs.data ?? [];
  const trend: EvaluationTrendPoint[] =
    summary.data?.trend?.length
      ? summary.data.trend
      : [...runList]
          .reverse()
          .map((run) => ({
            date: run.name ?? formatDate(run.startedAt),
            recall: numeric(run.metrics.recallAtK ?? run.metrics.averageRecall),
            mrr: numeric(run.metrics.mrr ?? run.metrics.averageMrr),
            faithfulness: numeric(run.metrics.faithfulness ?? run.metrics.averageFaithfulness),
            latencyMs: numeric(run.metrics.latencyMs ?? run.metrics.averageLatencyMs),
          }));

  const runColumns: Column<EvaluationRun>[] = [
    {
      key: 'name',
      header: 'Run',
      render: (run) => (
        <div>
          <p className="font-medium text-neutral-800 dark:text-neutral-100">{run.name ?? 'Evaluation'}</p>
          <p className="text-xs text-neutral-400">{run.datasetVersion ?? 'default dataset'}</p>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (run) => {
        const value = run.status.toLowerCase();
        const variant: BadgeVariant = value.includes('fail')
          ? 'danger'
          : value.includes('running')
            ? 'warning'
            : 'success';
        return <Badge variant={variant} dot className="capitalize">{run.status}</Badge>;
      },
    },
    {
      key: 'recall',
      header: 'Recall@k',
      render: (run) => <span className="text-neutral-600 dark:text-neutral-300">{formatPercent(run.metrics.recallAtK ?? run.metrics.averageRecall)}</span>,
    },
    {
      key: 'mrr',
      header: 'MRR',
      render: (run) => <span className="text-neutral-600 dark:text-neutral-300">{formatPercent(run.metrics.mrr ?? run.metrics.averageMrr)}</span>,
    },
    {
      key: 'faithfulness',
      header: 'Faithfulness',
      render: (run) => <span className="text-neutral-600 dark:text-neutral-300">{formatPercent(run.metrics.faithfulness ?? run.metrics.averageFaithfulness)}</span>,
    },
    {
      key: 'latency',
      header: 'Latency',
      render: (run) => <span className="text-neutral-600 dark:text-neutral-300">{formatDuration(run.metrics.latencyMs ?? run.metrics.averageLatencyMs)}</span>,
    },
    {
      key: 'started',
      header: 'Started',
      render: (run) => <span className="whitespace-nowrap text-neutral-500 dark:text-neutral-400">{formatDate(run.startedAt)}</span>,
    },
  ];

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight text-neutral-900 dark:text-white">Evaluation</h2>
          <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
            Track retrieval quality, groundedness, and response speed over time.
          </p>
        </div>
        <Button
          onClick={() => void start.mutate({ name: 'web-dashboard' })}
          isLoading={start.isPending}
          leftIcon={<Play className="h-4 w-4" />}
        >
          Run evaluation
        </Button>
      </div>

      <section aria-label="Evaluation metrics" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Recall@k" value={formatPercent(recall)} icon={<TrendingUp className="h-5 w-5" />} helper="Average across completed runs" tone="primary" />
        <StatCard label="Mean reciprocal rank" value={formatPercent(mrr)} icon={<BarChart3 className="h-5 w-5" />} helper="Ranking quality of citations" tone="purple" />
        <StatCard label="Faithfulness" value={formatPercent(faithfulness)} icon={<Gauge className="h-5 w-5" />} helper="Answers supported by sources" tone="success" />
        <StatCard label="Avg. latency" value={formatDuration(latency)} icon={<Timer className="h-5 w-5" />} helper={p95 ? `P95 ${formatDuration(p95)}` : 'Response time'} tone="warning" />
      </section>

      {summary.data?.totalRuns === 0 && trend.length === 0 && (
        <div className="rounded-xl border border-dashed border-neutral-300 p-4 text-center text-sm text-neutral-500 dark:border-neutral-700 dark:text-neutral-400">
          No evaluations have completed yet. Run one to populate these charts.
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-5">
        <Card className="xl:col-span-3">
          <CardHeader>
            <div>
              <CardTitle>Quality trends</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Recall, MRR, and faithfulness by run.</p>
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-72 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={trend} margin={{ top: 8, right: 12, bottom: 0, left: -18 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e5e5" className="dark:opacity-20" />
                  <XAxis dataKey="date" tick={axisStyle} tickLine={false} axisLine={false} interval="preserveStartEnd" />
                  <YAxis tick={axisStyle} tickLine={false} axisLine={false} domain={[0, 1]} tickFormatter={(value: number) => `${Math.round(value * 100)}%`} />
                  <Tooltip
                    formatter={(value, name) => [formatPercent(Number(value)), String(name)]}
                    contentStyle={{ borderRadius: 12, border: '1px solid #e5e5e5', fontSize: 12 }}
                  />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Line type="monotone" dataKey="recall" name="Recall@k" stroke="#0284c7" strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="mrr" name="MRR" stroke="#8b5cf6" strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="faithfulness" name="Faithfulness" stroke="#10b981" strokeWidth={2} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        <Card className="xl:col-span-2">
          <CardHeader>
            <div>
              <CardTitle>Latency by run</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Milliseconds per evaluation.</p>
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-72 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={trend} margin={{ top: 8, right: 8, bottom: 0, left: -18 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e5e5" className="dark:opacity-20" />
                  <XAxis dataKey="date" tick={axisStyle} tickLine={false} axisLine={false} interval="preserveStartEnd" />
                  <YAxis tick={axisStyle} tickLine={false} axisLine={false} tickFormatter={(value: number) => `${value}ms`} />
                  <Tooltip
                    formatter={(value) => [formatDuration(Number(value)), 'Latency']}
                    contentStyle={{ borderRadius: 12, border: '1px solid #e5e5e5', fontSize: 12 }}
                  />
                  <Bar dataKey="latencyMs" name="Latency" fill="#0ea5e9" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Run history</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
              {summary.data?.totalRuns ?? runList.length} total runs recorded
            </p>
          </div>
          <Activity className="h-5 w-5 text-neutral-400" />
        </CardHeader>
        <CardContent>
          <Table
            columns={runColumns}
            data={runList}
            keyExtractor={(run) => run.id}
            isLoading={runs.isLoading}
            emptyTitle="No runs recorded"
            emptyDescription="Start an evaluation to see results here."
          />
        </CardContent>
      </Card>

      {health.data?.checks && health.data.checks.length > 0 && (
        <p className="text-xs text-neutral-400">
          Evaluation services:{' '}
          {health.data.checks.map((check) => `${check.name} ${check.status}`).join(' · ')}
        </p>
      )}
    </div>
  );
}
