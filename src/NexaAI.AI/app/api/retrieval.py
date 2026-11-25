"""Document retrieval endpoints."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.core.logging import get_logger
from app.models.schemas import SearchRequest, SearchResponse
from app.services.dependencies import get_retrieval_service
from app.services.retrieval import RetrievalService

router = APIRouter()
logger = get_logger(__name__)


@router.post("/search", response_model=SearchResponse, summary="Search documents")
async def search(
    request: SearchRequest,
    service: RetrievalService = Depends(get_retrieval_service),
) -> SearchResponse:
    """Return permission-filtered chunks ranked by semantic similarity."""
    logger.info(
        "Document search request",
        top_k=request.top_k,
        user_id=str(request.user_id) if request.user_id else None,
    )
    return await service.search(request)


__all__ = ["router"]
