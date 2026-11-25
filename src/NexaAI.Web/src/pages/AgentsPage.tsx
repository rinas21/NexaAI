import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Activity,
  Bot,
  Calculator,
  Check,
  Clock3,
  Code2,
  Database,
  Globe2,
  Play,
  Search,
  ShieldCheck,
  Wrench,
  X,
} from 'lucide-react';
import { api } from '@services/api';
import { Badge, type BadgeVariant } from '@components/ui/Badge';
import { Button } from '@components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@components/ui/Card';
import { EmptyState } from '@components/ui/EmptyState';
import { Table, type Column } from '@components/ui/Table';
import { cn } from '@utils/cn';
import { formatDuration, formatRelative } from '@utils/format';
import type { AgentExecution, AgentExecutionStatus, ToolApproval, ToolDefinition } from '@/types';

const AGENTS = ['Research Assistant', 'Data Analyst', 'General Agent'];

const toolIcons: Record<string, typeof Search> = {
  search: Search,
  web: Globe2,
  web_search: Globe2,
  database: Database,
  sql: Database,
  calculator: Calculator,
  code: Code2,
  python: Code2,
};

function statusVariant(status: AgentExecutionStatus): BadgeVariant {
  const value = status.toLowerCase();
  if (value.includes('completed') || value.includes('success')) return 'success';
  if (value.includes('running') || value.includes('pending') || value.includes('awaiting')) return 'warning';
  if (value.includes('failed') || value.includes('cancel')) return 'danger';
  return 'neutral';
}

export function AgentsPage() {
  const queryClient = useQueryClient();
  const [agentName, setAgentName] = useState(AGENTS[0]);
  const [input, setInput] = useState('');
  const [requireApproval, setRequireApproval] = useState(false);
  const [result, setResult] = useState<AgentExecution | null>(null);

  const executions = useQuery({ queryKey: ['agents', 'executions'], queryFn: () => api.agents.list() });
  const tools = useQuery({ queryKey: ['agents', 'tools'], queryFn: () => api.agents.tools() });
  const approvals = useQuery({ queryKey: ['agents', 'approvals'], queryFn: () => api.agents.approvals() });

  const execute = useMutation({
    mutationFn: () => api.agents.execute({ agentName, input: input.trim(), requireApproval }),
    onSuccess: (execution) => {
      setResult(execution);
      void queryClient.invalidateQueries({ queryKey: ['agents'] });
    },
  });
  const approve = useMutation({
    mutationFn: (id: string) => api.agents.approve(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['agents', 'approvals'] }),
  });
  const reject = useMutation({
    mutationFn: (id: string) => api.agents.reject(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['agents', 'approvals'] }),
  });

  const executionColumns: Column<AgentExecution>[] = [
    {
      key: 'agent',
      header: 'Agent',
      render: (execution) => (
        <span className="flex items-center gap-2 font-medium text-neutral-800 dark:text-neutral-100">
          <Bot className="h-4 w-4 text-primary-600 dark:text-primary-400" />
          {execution.agentName ?? 'Agent'}
        </span>
      ),
    },
    {
      key: 'input',
      header: 'Input',
      render: (execution) => {
        const value = execution.query ?? (typeof execution.input === 'string' ? execution.input : 'Execution');
        return <span className="block max-w-[320px] truncate text-neutral-600 dark:text-neutral-300">{value}</span>;
      },
    },
    {
      key: 'status',
      header: 'Status',
      render: (execution) => (
        <Badge variant={statusVariant(execution.status)} dot className="capitalize">
          {execution.status}
        </Badge>
      ),
    },
    {
      key: 'duration',
      header: 'Duration',
      render: (execution) => (
        <span className="text-neutral-500 dark:text-neutral-400">
          {formatDuration(execution.executionTimeMs ?? execution.durationMs)}
        </span>
      ),
    },
    {
      key: 'date',
      header: 'Started',
      render: (execution) => (
        <span className="whitespace-nowrap text-neutral-500 dark:text-neutral-400">
          {formatRelative(execution.startedAt ?? execution.createdAt)}
        </span>
      ),
    },
  ];

  const pendingApprovals = (approvals.data ?? []).filter((approval) =>
    approval.status.toLowerCase().includes('pending'),
  );

  return (
    <div className="space-y-6 animate-fade-in">
      <div>
        <h2 className="text-2xl font-semibold tracking-tight text-neutral-900 dark:text-white">Agents</h2>
        <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
          Orchestrate tools, review actions, and inspect every run.
        </p>
      </div>

      <div className="grid gap-6 xl:grid-cols-3">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>Run an agent</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <label htmlFor="agent-name" className="label">Agent</label>
              <select
                id="agent-name"
                className="input cursor-pointer"
                value={agentName}
                onChange={(event) => setAgentName(event.target.value)}
              >
                {AGENTS.map((agent) => (
                  <option key={agent} value={agent}>{agent}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="agent-input" className="label">Task</label>
              <textarea
                id="agent-input"
                className="textarea"
                rows={4}
                placeholder="e.g. Summarize the Q3 reports and highlight the top three risks…"
                value={input}
                onChange={(event) => setInput(event.target.value)}
              />
            </div>
            <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-neutral-200 p-3 text-sm dark:border-neutral-700">
              <input
                type="checkbox"
                checked={requireApproval}
                onChange={(event) => setRequireApproval(event.target.checked)}
                className="h-4 w-4 rounded border-neutral-300 text-primary-600 focus:ring-primary-500"
              />
              <span className="flex items-center gap-2 text-neutral-700 dark:text-neutral-200">
                <ShieldCheck className="h-4 w-4 text-primary-600" />
                Require approval before tool use
              </span>
            </label>
            <div className="flex items-center gap-3">
              <Button
                onClick={() => void execute.mutate()}
                disabled={!input.trim()}
                isLoading={execute.isPending}
                leftIcon={<Play className="h-4 w-4" />}
              >
                Execute agent
              </Button>
              <span className="text-xs text-neutral-400">Runs are recorded below.</span>
            </div>

            {result && (
              <div className="rounded-xl border border-primary-200 bg-primary-50/70 p-4 dark:border-primary-900/50 dark:bg-primary-900/15">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-primary-900 dark:text-primary-200">Latest result</p>
                  <Badge variant={statusVariant(result.status)}>{result.status}</Badge>
                </div>
                <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-neutral-700 dark:text-neutral-300">
                  {result.result ?? result.errorMessage ?? 'Execution finished.'}
                </p>
                <p className="mt-3 text-xs text-primary-700/70 dark:text-primary-300/70">
                  {formatDuration(result.executionTimeMs ?? result.durationMs)} · {formatRelative(result.completedAt ?? result.createdAt)}
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Pending approvals</CardTitle>
              <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">Review sensitive tool calls.</p>
            </div>
            {pendingApprovals.length > 0 && <Badge variant="warning">{pendingApprovals.length} pending</Badge>}
          </CardHeader>
          <CardContent className="space-y-3">
            {pendingApprovals.length === 0 ? (
              <EmptyState
                icon={<ShieldCheck className="h-6 w-6" />}
                title="All clear"
                description="No tool calls are waiting for your approval."
              />
            ) : (
              pendingApprovals.map((approval: ToolApproval) => (
                <div key={approval.id} className="rounded-xl border border-amber-200 bg-amber-50/60 p-4 dark:border-amber-900/40 dark:bg-amber-900/10">
                  <div className="flex items-center gap-2">
                    <Wrench className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                    <p className="text-sm font-semibold text-neutral-800 dark:text-neutral-100">{approval.toolName}</p>
                  </div>
                  <p className="mt-1.5 line-clamp-3 text-xs text-neutral-600 dark:text-neutral-400">
                    {JSON.stringify(approval.parameters ?? {})}
                  </p>
                  <p className="mt-2 text-[11px] text-neutral-400">
                    Requested {formatRelative(approval.requestedAt ?? approval.createdAt)}
                  </p>
                  <div className="mt-3 flex gap-2">
                    <Button
                      size="sm"
                      leftIcon={<Check className="h-3.5 w-3.5" />}
                      isLoading={approve.isPending}
                      onClick={() => void approve.mutate(approval.id)}
                    >
                      Approve
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<X className="h-3.5 w-3.5" />}
                      disabled={reject.isPending}
                      onClick={() => void reject.mutate(approval.id)}
                    >
                      Reject
                    </Button>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Available tools</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
              Capabilities exposed to agents in this workspace.
            </p>
          </div>
        </CardHeader>
        <CardContent>
          {tools.isLoading ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {[0, 1, 2].map((index) => (
                <div key={index} className="skeleton h-32 rounded-xl" />
              ))}
            </div>
          ) : (tools.data ?? []).length === 0 ? (
            <EmptyState title="No tools registered" description="Tools connected to MCP appear here." icon={<Wrench className="h-6 w-6" />} />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {tools.data?.map((tool: ToolDefinition) => {
                const Icon = toolIcons[tool.name.toLowerCase()] ?? Wrench;
                return (
                  <div key={tool.name} className={cn('card-hover p-4', tool.enabled === false && 'opacity-60')}>
                    <div className="flex items-start justify-between gap-3">
                      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-300">
                        <Icon className="h-5 w-5" />
                      </span>
                      {tool.requiresApproval ? (
                        <Badge variant="warning">Approval required</Badge>
                      ) : (
                        <Badge variant="success">Auto</Badge>
                      )}
                    </div>
                    <p className="mt-3 font-medium text-neutral-800 dark:text-neutral-100">{tool.name}</p>
                    <p className="mt-1 text-sm leading-relaxed text-neutral-500 dark:text-neutral-400">
                      {tool.description}
                    </p>
                    {tool.category && <p className="mt-3 text-[11px] uppercase tracking-wide text-neutral-400">{tool.category}</p>}
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Execution history</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">A timeline of all agent runs.</p>
          </div>
          <Activity className="h-5 w-5 text-neutral-400" />
        </CardHeader>
        <CardContent>
          <Table
            columns={executionColumns}
            data={executions.data ?? []}
            keyExtractor={(execution) => execution.id}
            isLoading={executions.isLoading}
            emptyTitle="No executions yet"
            emptyDescription="Run an agent above to see its history here."
            emptyIcon={<Clock3 className="h-6 w-6" />}
          />
        </CardContent>
      </Card>
    </div>
  );
}
