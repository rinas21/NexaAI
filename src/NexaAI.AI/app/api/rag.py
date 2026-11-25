"""RAG chat endpoint."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.core.logging import get_logger
from app.models.schemas import RagRequest, RagResponse
from app.services.dependencies import get_rag_service
from app.services.rag import RagService

router = APIRouter()
logger = get_logger(__name__)


@router.post("/chat", response_model=RagResponse, summary="Grounded RAG chat")
async def chat(
    request: RagRequest,
    service: RagService = Depends(get_rag_service),
) -> RagResponse:
    """Answer a question grounded in the user's accessible documents."""
    logger.info(
        "RAG chat request",
        conversation_id=str(request.conversation_id) if request.conversation_id else None,
        history_turns=len(request.history),
    )
    return await service.chat(request)


__all__ = ["router"]
