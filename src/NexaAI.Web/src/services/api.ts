import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';
import type {
  AgentExecution,
  ApiError,
  ApprovalDecisionRequest,
  AuthResponse,
  ChatResponse,
  Conversation,
  CreateConversationRequest,
  Document,
  DocumentChunk,
  DocumentSearchResponse,
  EvaluationMetrics,
  EvaluationResult,
  EvaluationRun,
  EvaluationSummary,
  ExecuteAgentRequest,
  HealthStatus,
  LoginRequest,
  RegisterRequest,
  SendMessageRequest,
  StartEvaluationRequest,
  ToolApproval,
  ToolDefinition,
  UpdateProfileRequest,
  User,
} from '@/types';

export const ACCESS_TOKEN_KEY = 'nexaai_access_token';
export const REFRESH_TOKEN_KEY = 'nexaai_refresh_token';
export const AUTH_STORAGE_KEY = 'nexaai-auth';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

export const apiClient = axios.create({
  baseURL,
  timeout: 120_000,
  headers: { 'Content-Type': 'application/json' },
});

function storage(): Storage | null {
  return typeof window === 'undefined' ? null : window.localStorage;
}

export const tokenStorage = {
  getAccessToken: (): string | null => storage()?.getItem(ACCESS_TOKEN_KEY) ?? null,
  getRefreshToken: (): string | null => storage()?.getItem(REFRESH_TOKEN_KEY) ?? null,
  setTokens: (accessToken: string, refreshToken?: string | null): void => {
    const store = storage();
    if (!store) return;
    store.setItem(ACCESS_TOKEN_KEY, accessToken);
    if (refreshToken) store.setItem(REFRESH_TOKEN_KEY, refreshToken);
    else store.removeItem(REFRESH_TOKEN_KEY);
  },
  clear: (): void => {
    const store = storage();
    if (!store) return;
    store.removeItem(ACCESS_TOKEN_KEY);
    store.removeItem(REFRESH_TOKEN_KEY);
  },
};

/** Hooks let the auth store react to a 401 without creating an import cycle. */
const unauthorizedHandlers = new Set<() => void>();

export function onUnauthorized(handler: () => void): () => void {
  unauthorizedHandlers.add(handler);
  return () => unauthorizedHandlers.delete(handler);
}

function camelize(key: string): string {
  return key.charAt(0).toLowerCase() + key.slice(1);
}

/** The API serializes PascalCase DTOs; normalize them to client camelCase. */
function normalizePayload(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(normalizePayload);
  if (
    value !== null &&
    typeof value === 'object' &&
    !(value instanceof Date) &&
    !(value instanceof Blob) &&
    !(value instanceof FormData) &&
    !(value instanceof ArrayBuffer)
  ) {
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [camelize(key), normalizePayload(entry)]),
    );
  }
  return value;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

/** Convert any thrown value into a predictable, UI-friendly error. */
export function normalizeError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const data = asRecord(error.response?.data);
    const fieldErrors = asRecord(data.errors);
    const normalizedErrors: Record<string, string[]> = {};
    Object.entries(fieldErrors).forEach(([key, value]) => {
      normalizedErrors[camelize(key)] = Array.isArray(value)
        ? value.map(String)
        : [String(value)];
    });
    const message =
      (typeof data.detail === 'string' && data.detail) ||
      (typeof data.title === 'string' && data.title) ||
      (typeof data.message === 'string' && data.message) ||
      error.message ||
      'Something went wrong. Please try again.';
    return {
      message,
      statusCode: error.response?.status ?? 0,
      code: typeof data.code === 'string' ? data.code : undefined,
      errors: Object.keys(normalizedErrors).length ? normalizedErrors : undefined,
    };
  }
  if (error instanceof Error) {
    return { message: error.message || 'Unexpected error', statusCode: 0 };
  }
  return { message: 'Unexpected error', statusCode: 0 };
}

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    response.data = normalizePayload(response.data);
    return response;
  },
  (error: unknown) => {
    const normalized = normalizeError(error);
    if (normalized.statusCode === 401) {
      tokenStorage.clear();
      storage()?.removeItem(AUTH_STORAGE_KEY);
      unauthorizedHandlers.forEach((handler) => handler());
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        window.location.assign('/login');
      }
    }
    return Promise.reject(normalized);
  },
);

async function request<T>(config: AxiosRequestConfig): Promise<T> {
  const response = await apiClient.request<T>(config);
  return response.data;
}

function aggregateMetrics(results: EvaluationResult[] = []): EvaluationMetrics {
  const sums: EvaluationMetrics = {};
  const counts: Partial<Record<keyof EvaluationMetrics, number>> = {};
  const add = (key: keyof EvaluationMetrics, value?: number) => {
    if (typeof value !== 'number') return;
    sums[key] = (sums[key] ?? 0) + value;
    counts[key] = (counts[key] ?? 0) + 1;
  };
  results.forEach((result) => {
    const metrics = result.metrics ?? {};
    add('recallAtK', metrics.recallAtK ?? metrics.averageRecall);
    add('mrr', metrics.mrr ?? metrics.averageMrr);
    add('faithfulness', metrics.faithfulness ?? metrics.averageFaithfulness);
    add('answerRelevance', metrics.answerRelevance ?? metrics.averageAnswerRelevance);
    add('latencyMs', metrics.latencyMs ?? metrics.averageLatencyMs);
  });
  const averaged: EvaluationMetrics = {};
  (Object.keys(sums) as (keyof EvaluationMetrics)[]).forEach((key) => {
    const sum = sums[key];
    const count = counts[key] ?? 0;
    if (typeof sum === 'number' && count > 0) averaged[key] = sum / count;
  });
  return averaged;
}

function normalizeExecution(execution: AgentExecution): AgentExecution {
  return {
    ...execution,
    query: execution.query ?? (typeof execution.input === 'string' ? execution.input : undefined),
    result: execution.result ?? (typeof execution.output === 'string' ? execution.output : undefined),
  };
}

export const api = {
  auth: {
    login: (data: LoginRequest): Promise<AuthResponse> =>
      request<AuthResponse>({ method: 'POST', url: '/auth/login', data }),
    register: (data: RegisterRequest): Promise<AuthResponse> =>
      request<AuthResponse>({ method: 'POST', url: '/auth/register', data }),
    me: (): Promise<User> => request<User>({ url: '/auth/me' }),
    refresh: (refreshToken?: string): Promise<AuthResponse> =>
      request<AuthResponse>({
        method: 'POST',
        url: '/auth/refresh',
        data: { refreshToken: refreshToken ?? tokenStorage.getRefreshToken() },
      }),
    logout: (): Promise<void> => request<void>({ method: 'POST', url: '/auth/logout' }),
    updateProfile: (data: UpdateProfileRequest): Promise<User> =>
      request<User>({ method: 'PUT', url: '/auth/profile', data }),
  },

  documents: {
    list: (params?: { search?: string; status?: string }): Promise<Document[]> =>
      request<Document[]>({ url: '/documents', params }),
    upload: (
      file: File,
      accessLevel = 'Private',
      onProgress?: (percent: number) => void,
    ): Promise<Document> => {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('title', file.name);
      formData.append('accessLevel', accessLevel);
      return request<Document>({
        method: 'POST',
        url: '/documents/upload',
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (event) => {
          if (!onProgress || !event.total) return;
          onProgress(Math.round((event.loaded / event.total) * 100));
        },
      });
    },
    get: (id: string): Promise<Document> => request<Document>({ url: `/documents/${id}` }),
    delete: (id: string): Promise<void> =>
      request<void>({ method: 'DELETE', url: `/documents/${id}` }),
    search: (query: string, topK = 10): Promise<DocumentChunk[]> =>
      request<DocumentChunk[] | DocumentSearchResponse>({
        url: '/documents/search',
        params: { query, topK },
      }).then((result) => (Array.isArray(result) ? result : (result.results ?? []))),
    updateAccess: (id: string, accessLevel: string): Promise<Document> =>
      request<Document>({
        method: 'PATCH',
        url: `/documents/${id}/access`,
        data: { accessLevel },
      }),
  },

  chat: {
    send: (data: SendMessageRequest): Promise<ChatResponse> =>
      request<ChatResponse>({ method: 'POST', url: '/chat', data }),
    conversations: {
      list: (): Promise<Conversation[]> => request<Conversation[]>({ url: '/chat/conversations' }),
      create: (data: CreateConversationRequest = {}): Promise<Conversation> =>
        request<Conversation>({ method: 'POST', url: '/chat/conversations', data }),
      get: (id: string): Promise<Conversation> =>
        request<Conversation>({ url: `/chat/conversations/${id}` }),
      delete: (id: string): Promise<void> =>
        request<void>({ method: 'DELETE', url: `/chat/conversations/${id}` }),
    },
  },

  agents: {
    execute: async (data: ExecuteAgentRequest): Promise<AgentExecution> =>
      normalizeExecution(await request<AgentExecution>({ method: 'POST', url: '/agents/execute', data })),
    list: async (): Promise<AgentExecution[]> =>
      (await request<AgentExecution[]>({ url: '/agents/executions' })).map(normalizeExecution),
    approvals: (): Promise<ToolApproval[]> =>
      request<ToolApproval[]>({ url: '/agents/approvals' }),
    approve: (id: string, data: ApprovalDecisionRequest = {}): Promise<ToolApproval> =>
      request<ToolApproval>({ method: 'POST', url: `/agents/approvals/${id}/approve`, data }),
    reject: (id: string, data: ApprovalDecisionRequest = {}): Promise<ToolApproval> =>
      request<ToolApproval>({ method: 'POST', url: `/agents/approvals/${id}/reject`, data }),
    tools: (): Promise<ToolDefinition[]> => request<ToolDefinition[]>({ url: '/agents/tools' }),
  },

  evaluation: {
    start: async (data: StartEvaluationRequest = {}): Promise<EvaluationRun> => {
      const run = await request<EvaluationRun>({ method: 'POST', url: '/evaluation/start', data });
      return { ...run, metrics: run.metrics ?? aggregateMetrics(run.results) };
    },
    runs: async (): Promise<EvaluationRun[]> =>
      (await request<EvaluationRun[]>({ url: '/evaluation/runs' })).map((run) => ({
        ...run,
        metrics: run.metrics ?? aggregateMetrics(run.results),
      })),
    summary: async (): Promise<EvaluationSummary> => {
      const data = await request<EvaluationSummary>({ url: '/evaluation/summary' });
      return {
        ...data,
        averageMetrics: data.averageMetrics ?? {
          recallAtK: data.averageRecall,
          mrr: data.averageMrr,
          faithfulness: data.averageFaithfulness,
          answerRelevance: data.averageAnswerRelevance,
          latencyMs: data.averageLatencyMs,
          p95LatencyMs: data.p95LatencyMs,
          totalTokensUsed: data.totalTokensUsed,
          totalFailures: data.totalFailures,
        },
      };
    },
  },

  health: {
    get: (): Promise<HealthStatus> =>
      request<HealthStatus>({ url: '/health', baseURL: '/' }),
    detailed: (): Promise<HealthStatus> =>
      request<HealthStatus>({ url: '/health/detailed', baseURL: '/' }),
  },
};
