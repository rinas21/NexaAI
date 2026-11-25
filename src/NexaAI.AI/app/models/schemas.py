"""Pydantic models for API request/response validation."""

from datetime import datetime
from enum import Enum
from typing import Any
from uuid import UUID, uuid4

from pydantic import BaseModel, Field


# =============================================================================
# Common Models
# =============================================================================

class HealthStatus(str, Enum):
    HEALTHY = "healthy"
    UNHEALTHY = "unhealthy"
    DEGRADED = "degraded"


class HealthResponse(BaseModel):
    status: HealthStatus
    version: str
    services: dict[str, str] = Field(default_factory=dict)
    timestamp: datetime = Field(default_factory=datetime.utcnow)


class ErrorResponse(BaseModel):
    error: str
    error_code: str
    message: str
    details: dict[str, Any] = Field(default_factory=dict)
    timestamp: datetime = Field(default_factory=datetime.utcnow)


# =============================================================================
# Embedding Models
# =============================================================================

class EmbedRequest(BaseModel):
    texts: list[str] = Field(..., min_length=1, max_length=100)
    model: str | None = None


class EmbedResponse(BaseModel):
    embeddings: list[list[float]]
    model: str
    dimension: int


# =============================================================================
# Retrieval Models
# =============================================================================

class SearchRequest(BaseModel):
    query: str = Field(..., min_length=1, max_length=10000)
    top_k: int = Field(default=5, ge=1, le=50)
    similarity_threshold: float = Field(default=0.65, ge=0.0, le=1.0)
    user_id: UUID | None = None
    access_level: str | None = None
    metadata_filter: dict[str, Any] | None = None


class SearchResult(BaseModel):
    chunk_id: UUID
    document_id: UUID
    document_title: str
    content: str
    similarity_score: float
    chunk_index: int
    metadata: dict[str, Any] = Field(default_factory=dict)


class SearchResponse(BaseModel):
    results: list[SearchResult]
    total_count: int
    search_time_ms: float


# =============================================================================
# RAG Chat Models
# =============================================================================

class ChatMessage(BaseModel):
    role: str = Field(..., pattern="^(user|assistant|system|tool)$")
    content: str


class Citation(BaseModel):
    document_id: UUID
    chunk_id: UUID
    document_title: str
    snippet: str
    similarity_score: float


class TokenUsage(BaseModel):
    prompt_tokens: int = 0
    completion_tokens: int = 0
    total_tokens: int = 0


class RagRequest(BaseModel):
    message: str = Field(..., min_length=1, max_length=10000)
    conversation_id: UUID | None = None
    user_id: UUID
    history: list[ChatMessage] = Field(default_factory=list)
    use_agent: bool = False
    temperature: float | None = None


class RagResponse(BaseModel):
    answer: str
    citations: list[Citation] = Field(default_factory=list)
    is_grounded: bool
    refusal_reason: str | None = None
    token_usage: TokenUsage = Field(default_factory=TokenUsage)
    latency_ms: float
    conversation_id: UUID | None = None


# =============================================================================
# Agent Models
# =============================================================================

class ToolCall(BaseModel):
    tool_name: str
    arguments: dict[str, Any]
    result: Any | None = None
    success: bool = False
    execution_time_ms: float = 0.0
    requires_approval: bool = False
    approval_id: str | None = None


class AgentRequest(BaseModel):
    input: str = Field(..., min_length=1, max_length=10000)
    user_id: UUID
    conversation_id: UUID | None = None
    tools: list[str] | None = None
    require_approval: bool = False
    max_iterations: int = Field(default=10, ge=1, le=50)


class AgentResponse(BaseModel):
    output: str
    tool_calls: list[ToolCall] = Field(default_factory=list)
    iterations: int
    latency_ms: float
    token_usage: TokenUsage = Field(default_factory=TokenUsage)
    status: str = "completed"


class ToolInfo(BaseModel):
    name: str
    description: str
    parameters: dict[str, Any] = Field(default_factory=dict)
    requires_approval: bool = False


# =============================================================================
# Security Models
# =============================================================================

class PiiEntity(BaseModel):
    entity_type: str
    start: int
    end: int
    score: float
    text: str


class PiiDetectRequest(BaseModel):
    text: str = Field(..., min_length=1, max_length=100000)
    language: str = "en"
    mask: bool = False


class PiiDetectResponse(BaseModel):
    original_text: str
    processed_text: str
    entities: list[PiiEntity]
    has_pii: bool


class SanitizeResponse(BaseModel):
    original_text: str
    sanitized_text: str
    is_safe: bool
    threats_detected: list[str] = Field(default_factory=list)


# =============================================================================
# Evaluation Models
# =============================================================================

class EvalSample(BaseModel):
    question_id: str
    question: str
    expected_answer: str
    relevant_doc_ids: list[str] = Field(default_factory=list)
    metadata: dict[str, Any] = Field(default_factory=dict)


class EvalRequest(BaseModel):
    samples: list[EvalSample] = Field(..., min_length=1)
    config: dict[str, Any] = Field(default_factory=dict)


class EvalMetrics(BaseModel):
    recall_at_k: float = 0.0
    mrr: float = 0.0
    ndcg: float = 0.0
    faithfulness: float = 0.0
    answer_relevance: float = 0.0
    answer_correctness: float = 0.0
    latency_ms: float = 0.0


class EvalResult(BaseModel):
    question_id: str
    question: str
    expected_answer: str
    actual_answer: str
    metrics: EvalMetrics
    retrieved_chunk_ids: list[str] = Field(default_factory=list)


class EvalReport(BaseModel):
    id: UUID = Field(default_factory=uuid4)
    created_at: datetime = Field(default_factory=datetime.utcnow)
    total_samples: int
    aggregate_metrics: EvalMetrics
    results: list[EvalResult]
    config: dict[str, Any] = Field(default_factory=dict)


# =============================================================================
# MCP Models
# =============================================================================

class McpToolCallRequest(BaseModel):
    name: str
    arguments: dict[str, Any] = Field(default_factory=dict)


class McpToolCallResponse(BaseModel):
    result: Any
    is_error: bool = False
    execution_time_ms: float = 0.0