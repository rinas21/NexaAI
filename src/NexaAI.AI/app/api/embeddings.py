"""Embedding endpoints backed by the local Ollama embedding model."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.config import settings
from app.core.exceptions import EmbeddingError
from app.core.logging import get_logger
from app.models.schemas import EmbedRequest, EmbedResponse
from app.services.dependencies import get_ollama_client
from app.services.ollama_client import OllamaClient

router = APIRouter()
logger = get_logger(__name__)


@router.post("/embed", response_model=EmbedResponse, summary="Generate embeddings")
async def embed(
    request: EmbedRequest,
    client: OllamaClient = Depends(get_ollama_client),
) -> EmbedResponse:
    """Embed one or more texts using Ollama (no external inference provider)."""
    model = request.model or settings.ollama_embedding_model
    try:
        embeddings = await client.generate_embeddings(request.texts, model=model)
    except Exception as exc:
        logger.error("Embedding request failed", error=str(exc), model=model)
        raise EmbeddingError(f"Embedding generation failed: {exc}") from exc
    return EmbedResponse(
        embeddings=embeddings,
        model=model,
        dimension=len(embeddings[0]) if embeddings else 0,
    )


__all__ = ["router"]
