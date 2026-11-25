"""Hybrid document retrieval: remote ASP.NET search with a local fallback.

The C# API owns pgvector and is the preferred search backend. This service
embeds the query locally with Ollama, asks the API for permission-filtered
candidate chunks, and re-ranks them locally by cosine similarity. When the API
is unavailable an in-memory store keeps standalone development and tests
working without any external service.
"""

from __future__ import annotations

import re
import time
from collections.abc import Iterable
from dataclasses import dataclass, field
from typing import Any
from uuid import UUID, uuid4

import httpx

from app.config import settings
from app.core.exceptions import EmbeddingError
from app.core.logging import get_logger
from app.models.schemas import (
    SearchRequest,
    SearchResponse,
    SearchResult,
)
from app.services.ollama_client import OllamaClient

logger = get_logger(__name__)

_ACCESS_RANK = {"public": 0, "internal": 1, "confidential": 2, "restricted": 3}
_TOKEN_RE = re.compile(r"[a-z0-9]+")
_STOPWORDS = {
    "a", "an", "and", "are", "as", "at", "be", "by", "do", "for", "from", "how",
    "i", "in", "is", "it", "of", "on", "or", "our", "that", "the", "this", "to",
    "was", "what", "when", "where", "which", "who", "why", "with", "you", "your",
}


@dataclass
class StoredChunk:
    """A document chunk kept in the standalone in-memory store."""

    chunk_id: str
    document_id: str
    document_title: str
    content: str
    chunk_index: int = 0
    embedding: list[float] | None = None
    metadata: dict[str, Any] = field(default_factory=dict)


LOCAL_CHUNKS: list[StoredChunk] = []
"""Module-level fallback store shared by routers, tools and MCP."""


def add_local_chunk(
    document_id: str,
    content: str,
    *,
    document_title: str = "Untitled",
    chunk_index: int = 0,
    chunk_id: str | None = None,
    embedding: list[float] | None = None,
    metadata: dict[str, Any] | None = None,
) -> StoredChunk:
    """Add a chunk to the in-memory fallback store and return it."""
    chunk = StoredChunk(
        chunk_id=chunk_id or str(uuid4()),
        document_id=str(document_id),
        document_title=document_title,
        content=content,
        chunk_index=chunk_index,
        embedding=embedding,
        metadata=metadata or {},
    )
    LOCAL_CHUNKS.append(chunk)
    return chunk


def clear_local_chunks() -> None:
    """Remove all chunks from the in-memory fallback store."""
    LOCAL_CHUNKS.clear()


def _tokens(text: str) -> set[str]:
    return {token for token in _TOKEN_RE.findall(text.lower()) if token not in _STOPWORDS}


def cosine_similarity(left: Iterable[float], right: Iterable[float]) -> float:
    """Compute cosine similarity, returning 0.0 for degenerate vectors."""
    a = list(left)
    b = list(right)
    if not a or not b or len(a) != len(b):
        return 0.0
    dot = sum(x * y for x, y in zip(a, b))
    norm_a = sum(x * x for x in a) ** 0.5
    norm_b = sum(y * y for y in b) ** 0.5
    if norm_a == 0 or norm_b == 0:
        return 0.0
    return max(-1.0, min(1.0, dot / (norm_a * norm_b)))


def lexical_similarity(query: str, content: str) -> float:
    """Token overlap score used when no embedding is attached to a chunk."""
    query_tokens = _tokens(query)
    content_tokens = _tokens(content)
    if not query_tokens or not content_tokens:
        return 0.0
    return len(query_tokens & content_tokens) / len(query_tokens)


def _as_uuid(value: Any) -> UUID:
    if isinstance(value, UUID):
        return value
    return UUID(str(value))


def _get(data: dict[str, Any], *names: str, default: Any = None) -> Any:
    """Read the first present key, accepting snake_case and camelCase."""
    for name in names:
        if name in data and data[name] is not None:
            return data[name]
    return default


def _normalize_candidate(item: dict[str, Any]) -> dict[str, Any]:
    metadata = _get(item, "metadata", "Metadata", default={}) or {}
    if not isinstance(metadata, dict):
        metadata = {}
    return {
        "chunk_id": str(_get(item, "chunkId", "chunk_id", default=uuid4())),
        "document_id": str(_get(item, "documentId", "document_id", default=uuid4())),
        "document_title": str(_get(item, "documentTitle", "document_title", "title", default="Untitled")),
        "content": str(_get(item, "content", "text", default="")),
        "chunk_index": int(_get(item, "chunkIndex", "chunk_index", default=0) or 0),
        "metadata": metadata,
        "embedding": _get(item, "embedding", "vector", "Embedding"),
        "score": _get(item, "similarityScore", "similarity_score", "score", "Score"),
    }


def _is_allowed(metadata: dict[str, Any], request: SearchRequest) -> bool:
    """Apply the coarse permission checks also enforced by the C# API."""
    required = str(metadata.get("access_level", "public")).lower()
    if request.access_level and _ACCESS_RANK.get(required, 0) > _ACCESS_RANK.get(
        request.access_level.lower(), 0
    ):
        return False

    allowed_users = metadata.get("allowed_user_ids")
    if allowed_users and request.user_id:
        if str(request.user_id) not in {str(user) for user in allowed_users}:
            return False

    owner = metadata.get("user_id")
    if owner and request.user_id and str(owner) != str(request.user_id):
        return False

    for key, expected in (request.metadata_filter or {}).items():
        actual = metadata.get(key)
        if isinstance(expected, (list, tuple, set)):
            if actual not in expected:
                return False
        elif str(actual) != str(expected):
            return False
    return True


class RetrievalService:
    """Retrieve permission-filtered chunks relevant to a query."""

    def __init__(
        self,
        ollama_client: OllamaClient | None = None,
        api_base_url: str | None = None,
        api_key: str | None = None,
        *,
        use_remote: bool = True,
        timeout_seconds: float = 5.0,
    ) -> None:
        self.ollama = ollama_client or OllamaClient()
        self.api_base_url = (api_base_url or settings.api_base_url).rstrip("/")
        self.api_key = api_key or settings.api_key
        self.use_remote = use_remote
        self.timeout_seconds = timeout_seconds

    async def _remote_search(self, request: SearchRequest, query_embedding: list[float]) -> list[dict[str, Any]] | None:
        """Ask the ASP.NET API for candidate chunks; ``None`` means unavailable."""
        if not self.use_remote:
            return None
        payload: dict[str, Any] = {
            "query": request.query,
            "embedding": query_embedding,
            "topK": request.top_k * 3,
            "similarityThreshold": max(0.0, request.similarity_threshold - 0.15),
        }
        if request.user_id:
            payload["userId"] = str(request.user_id)
        if request.access_level:
            payload["accessLevel"] = request.access_level
        if request.metadata_filter:
            payload["metadataFilter"] = request.metadata_filter
        try:
            async with httpx.AsyncClient(base_url=self.api_base_url, timeout=self.timeout_seconds) as client:
                response = await client.post(
                    "/api/documents/search",
                    json=payload,
                    headers={"X-API-Key": self.api_key},
                )
                response.raise_for_status()
                data = response.json()
        except (httpx.HTTPError, ValueError) as exc:
            logger.warning("Remote document search unavailable; using local store", error=str(exc))
            return None

        if isinstance(data, list):
            candidates = data
        elif isinstance(data, dict):
            candidates = _get(data, "results", "items", "chunks", default=[]) or []
        else:
            candidates = []
        if not isinstance(candidates, list):
            return None
        logger.info("Remote document search returned candidates", count=len(candidates))
        return [_normalize_candidate(item) for item in candidates if isinstance(item, dict)]

    def _local_candidates(self) -> list[dict[str, Any]]:
        return [
            {
                "chunk_id": chunk.chunk_id,
                "document_id": chunk.document_id,
                "document_title": chunk.document_title,
                "content": chunk.content,
                "chunk_index": chunk.chunk_index,
                "metadata": chunk.metadata,
                "embedding": chunk.embedding,
                "score": None,
            }
            for chunk in LOCAL_CHUNKS
        ]

    def _score(
        self,
        candidate: dict[str, Any],
        query: str,
        query_embedding: list[float],
    ) -> float:
        embedding = candidate.get("embedding")
        if embedding:
            score = cosine_similarity(query_embedding, embedding)
            # Embedding models are not calibrated across queries; blend a small
            # lexical signal so exact keyword matches are never discarded.
            return 0.85 * score + 0.15 * lexical_similarity(query, candidate["content"])
        if candidate.get("score") is not None:
            try:
                return float(candidate["score"])
            except (TypeError, ValueError):
                pass
        return lexical_similarity(query, candidate["content"])

    def _rank(
        self,
        candidates: list[dict[str, Any]],
        request: SearchRequest,
        query_embedding: list[float],
    ) -> list[SearchResult]:
        scored: list[tuple[float, dict[str, Any]]] = []
        seen: set[str] = set()
        for candidate in candidates:
            if not _is_allowed(candidate["metadata"], request):
                continue
            key = candidate["chunk_id"]
            if key in seen:
                continue
            seen.add(key)
            score = self._score(candidate, request.query, query_embedding)
            if score >= request.similarity_threshold:
                scored.append((score, candidate))

        scored.sort(key=lambda item: item[0], reverse=True)
        results: list[SearchResult] = []
        for score, candidate in scored[: request.top_k]:
            try:
                chunk_id = _as_uuid(candidate["chunk_id"])
                document_id = _as_uuid(candidate["document_id"])
            except (ValueError, AttributeError, TypeError) as exc:
                logger.warning("Skipping candidate with invalid identifiers", error=str(exc))
                continue
            results.append(
                SearchResult(
                    chunk_id=chunk_id,
                    document_id=document_id,
                    document_title=candidate["document_title"],
                    content=candidate["content"],
                    similarity_score=round(score, 6),
                    chunk_index=candidate["chunk_index"],
                    metadata=candidate["metadata"],
                )
            )
        return results

    async def search(self, request: SearchRequest) -> SearchResponse:
        """Embed the query, retrieve candidates and return ranked results."""
        started = time.perf_counter()
        try:
            query_embedding = await self.ollama.generate_embedding(request.query)
        except Exception as exc:
            raise EmbeddingError(f"Failed to embed search query: {exc}") from exc

        candidates = await self._remote_search(request, query_embedding)
        if not candidates:
            candidates = self._local_candidates()
        if not candidates:
            logger.info("No document candidates available", query=request.query[:120])

        results = self._rank(candidates, request, query_embedding)
        return SearchResponse(
            results=results,
            total_count=len(results),
            search_time_ms=(time.perf_counter() - started) * 1000,
        )

    async def search_chunks(
        self,
        query: str,
        *,
        top_k: int | None = None,
        user_id: UUID | None = None,
        access_level: str | None = None,
        metadata_filter: dict[str, Any] | None = None,
        similarity_threshold: float | None = None,
    ) -> list[SearchResult]:
        """Convenience wrapper returning only the ranked results."""
        request = SearchRequest(
            query=query,
            top_k=top_k or settings.rag_top_k,
            similarity_threshold=(
                similarity_threshold
                if similarity_threshold is not None
                else settings.rag_similarity_threshold
            ),
            user_id=user_id,
            access_level=access_level,
            metadata_filter=metadata_filter,
        )
        response = await self.search(request)
        return response.results


__all__ = [
    "LOCAL_CHUNKS",
    "StoredChunk",
    "RetrievalService",
    "add_local_chunk",
    "clear_local_chunks",
    "cosine_similarity",
    "lexical_similarity",
]
