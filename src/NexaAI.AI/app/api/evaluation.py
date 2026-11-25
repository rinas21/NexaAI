"""RAG evaluation endpoint."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.core.logging import get_logger
from app.evaluation.runner import EvaluationRunner
from app.models.schemas import EvalReport, EvalRequest
from app.services.dependencies import get_evaluation_runner

router = APIRouter()
logger = get_logger(__name__)


@router.post("/run", response_model=EvalReport, summary="Run a RAG evaluation dataset")
async def run(
    request: EvalRequest,
    runner: EvaluationRunner = Depends(get_evaluation_runner),
) -> EvalReport:
    """Run the RAG pipeline over labelled samples and aggregate quality metrics."""
    logger.info("Evaluation run requested", samples=len(request.samples))
    return await runner.run(request)


__all__ = ["router"]
