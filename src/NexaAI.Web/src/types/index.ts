/** Shared domain types for the NexaAI web client. */

export type UserRole = 'Admin' | 'User' | 'Viewer' | (string & {});

export interface User {
  id: string;
  email: string;
  fullName?: string | null;
  name?: string;
  role: UserRole;
  isActive?: boolean;
  createdAt?: string;
  avatarUrl?: string;
}

export interface AuthResponse {
  accessToken: string;
  /** Some deployments return `token` instead of `accessToken`. */
  token?: string;
  refreshToken: string;
  expiresAt?: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
}

export interface UpdateProfileRequest {
  fullName?: string;
  email?: string;
}

export type AccessLevel = 'Private' | 'Team' | 'Public' | (string & {});
export type DocumentStatus =
  | 'Pending'
  | 'Uploaded'
  | 'Processing'
  | 'Indexed'
  | 'Ready'
  | 'Failed'
  | (string & {});

export interface DocumentChunk {
  id: string;
  documentId?: string;
  chunkIndex: number;
  content: string;
  tokenCount?: number;
  pageNumber?: number;
  similarityScore?: number;
  score?: number;
  metadata?: Record<string, unknown>;
}

export interface Document {
  id: string;
  title: string;
  originalFilename?: string;
  fileName?: string;
  contentType?: string;
  fileSize: number;
  status: DocumentStatus;
  chunkCount?: number;
  accessLevel?: AccessLevel;
  ownerId?: string;
  ownerName?: string;
  createdAt: string;
  updatedAt?: string;
  metadata?: Record<string, unknown>;
  errorMessage?: string;
  chunks?: DocumentChunk[];
}

export type MessageRole = 'user' | 'assistant' | 'system' | 'tool' | (string & {});

export interface Citation {
  id?: string;
  documentId: string;
  chunkId?: string;
  documentTitle: string;
  snippet?: string;
  similarityScore?: number;
  score?: number;
  page?: number;
}

export interface ToolCall {
  toolName: string;
  arguments?: string | Record<string, unknown>;
  result?: string | null;
  success?: boolean;
  executionTimeMs?: number;
}

export interface ChatMessage {
  id: string;
  conversationId?: string;
  role: MessageRole;
  content: string;
  createdAt: string;
  citations?: Citation[];
  toolCalls?: ToolCall[];
  isGrounded?: boolean;
  refusalReason?: string;
  latencyMs?: number;
  /** Client-only flag used while a mocked token stream is rendering. */
  isStreaming?: boolean;
}

export interface Conversation {
  id: string;
  title?: string | null;
  createdAt: string;
  updatedAt: string;
  messages?: ChatMessage[];
  messageCount?: number;
}

export interface SendMessageRequest {
  message: string;
  conversationId?: string | null;
  useAgent?: boolean;
  metadata?: Record<string, unknown>;
}

export interface ChatResponse {
  conversationId: string;
  messageId: string;
  content: string;
  citations?: Citation[];
  isGrounded?: boolean;
  refusalReason?: string;
  totalLatencyMs?: number;
}

export interface CreateConversationRequest {
  title?: string;
}

export type AgentExecutionStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'AwaitingApproval'
  | (string & {});

export interface AgentStep {
  id?: string;
  type: 'thought' | 'tool' | 'observation' | 'answer' | (string & {});
  content: string;
  toolName?: string;
  timestamp?: string;
}

export interface AgentExecution {
  id: string;
  agentName?: string;
  query?: string;
  input?: string | Record<string, unknown>;
  status: AgentExecutionStatus;
  createdAt?: string;
  startedAt?: string;
  completedAt?: string | null;
  executionTimeMs?: number;
  durationMs?: number;
  result?: string;
  output?: unknown;
  errorMessage?: string;
  error?: string;
  toolsUsed?: string[];
  steps?: AgentStep[];
}

export interface ExecuteAgentRequest {
  agentName: string;
  input: string;
  requireApproval?: boolean;
  parameters?: Record<string, unknown>;
}

export type ApprovalStatus =
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Expired'
  | (string & {});

export interface ToolApproval {
  id: string;
  executionId: string;
  toolName: string;
  parameters?: Record<string, unknown>;
  reason?: string | null;
  status: ApprovalStatus;
  createdAt?: string;
  requestedAt?: string;
  decidedAt?: string | null;
}

export interface ApprovalDecisionRequest {
  reason?: string;
}

export interface ToolDefinition {
  name: string;
  description: string;
  parameters?: Record<string, unknown>;
  requiresApproval: boolean;
  category?: string;
  enabled?: boolean;
}

export interface EvaluationMetrics {
  recallAtK?: number;
  averageRecall?: number;
  mrr?: number;
  averageMrr?: number;
  faithfulness?: number;
  averageFaithfulness?: number;
  answerRelevance?: number;
  averageAnswerRelevance?: number;
  contextPrecision?: number;
  latencyMs?: number;
  averageLatencyMs?: number;
  p95LatencyMs?: number;
  totalTokensUsed?: number;
  totalFailures?: number;
}

export interface EvaluationResult {
  id?: string;
  questionId?: string;
  question?: string;
  expectedAnswer?: string | null;
  actualAnswer?: string | null;
  metrics?: EvaluationMetrics;
  createdAt?: string;
}

export type EvaluationRunStatus = 'Running' | 'Completed' | 'Failed' | (string & {});

export interface EvaluationRun {
  id: string;
  name?: string;
  datasetVersion?: string | null;
  status: EvaluationRunStatus;
  startedAt: string;
  completedAt?: string | null;
  metrics: EvaluationMetrics;
  results?: EvaluationResult[];
  sampleCount?: number;
}

export interface EvaluationTrendPoint {
  date: string;
  recall: number;
  mrr: number;
  faithfulness: number;
  latencyMs: number;
}

export interface EvaluationSummary {
  totalRuns: number;
  averageMetrics: EvaluationMetrics;
  /** Flat averages returned by some API versions. */
  averageRecall?: number;
  averageMrr?: number;
  averageFaithfulness?: number;
  averageAnswerRelevance?: number;
  averageLatencyMs?: number;
  p95LatencyMs?: number;
  totalTokensUsed?: number;
  totalFailures?: number;
  trend?: EvaluationTrendPoint[];
  latestRuns?: EvaluationRun[];
}

export interface StartEvaluationRequest {
  name?: string;
  datasetVersion?: string;
  config?: Record<string, unknown>;
}

export interface ApiError {
  message: string;
  statusCode: number;
  code?: string;
  errors?: Record<string, string[]>;
}

export interface HealthCheck {
  name: string;
  status: string;
  description?: string;
  duration?: number;
}

export interface HealthStatus {
  status: string;
  checks?: HealthCheck[];
}

export interface DocumentSearchResponse {
  results: DocumentChunk[];
  totalCount?: number;
  searchTime?: number | string;
}
