"""Evaluation runner that executes a dataset through the RAG pipeline."""

from __future__ import annotations

from statistics import fmean
from uuid import UUID

from app.config import settings
from app.core.logging import get_logger
from app.evaluation.metrics import (
    answer_correctness,
    answer_relevance,
    faithfulness,
    mrr,
    ndcg,
    recall_at_k,
)
from app.models.schemas import (
    EvalMetrics,
    EvalReport,
    EvalRequest,
    EvalResult,
    RagRequest,
)
from app.services.rag import RagService
from app.services.retrieval import RetrievalService

logger = get_logger(__name__)

EVAL_USER_ID = UUID("00000000-0000-0000-0000-000000000001")


class EvaluationRunner:
    """Run retrieval/generation evaluations and aggregate their metrics."""

    def __init__(
        self,
        rag_service: RagService | None = None,
        retrieval_service: RetrievalService | None = None,
    ) -> None:
        self.rag = rag_service or RagService()
        self.retrieval = retrieval_service or RetrievalService()

    async def run(self, request: EvalRequest) -> EvalReport:
        """Evaluate every sample and return an aggregate report."""
        k = int(request.config.get("top_k", settings.rag_top_k))
        results: list[EvalResult] = []

        for sample in request.samples:
            rag_response = await self.rag.chat(
                RagRequest(
                    message=sample.question,
                    user_id=EVAL_USER_ID,
                    temperature=request.config.get("temperature"),
                )
            )
            retrieved_ids = [str(citation.document_id) for citation in rag_response.citations]
            chunk_ids = [str(citation.chunk_id) for citation in rag_response.citations]
            contexts = [citation.snippet for citation in rag_response.citations]
            relevant = sample.relevant_doc_ids

            metrics = EvalMetrics(
                recall_at_k=recall_at_k(retrieved_ids, relevant, k=k),
                mrr=mrr(retrieved_ids, relevant),
                ndcg=ndcg(retrieved_ids, relevant, k=k),
                faithfulness=faithfulness(rag_response.answer, contexts),
                answer_relevance=answer_relevance(rag_response.answer, sample.question),
                answer_correctness=answer_correctness(rag_response.answer, sample.expected_answer),
                latency_ms=rag_response.latency_ms,
            )
            results.append(
                EvalResult(
                    question_id=sample.question_id,
                    question=sample.question,
                    expected_answer=sample.expected_answer,
                    actual_answer=rag_response.answer,
                    metrics=metrics,
                    retrieved_chunk_ids=chunk_ids,
                )
            )

        aggregate = EvalMetrics(
            recall_at_k=fmean(result.metrics.recall_at_k for result in results),
            mrr=fmean(result.metrics.mrr for result in results),
            ndcg=fmean(result.metrics.ndcg for result in results),
            faithfulness=fmean(result.metrics.faithfulness for result in results),
            answer_relevance=fmean(result.metrics.answer_relevance for result in results),
            answer_correctness=fmean(result.metrics.answer_correctness for result in results),
            latency_ms=fmean(result.metrics.latency_ms for result in results),
        )
        logger.info("Evaluation complete", samples=len(results))
        return EvalReport(
            total_samples=len(results),
            aggregate_metrics=aggregate,
            results=results,
            config=request.config,
        )


__all__ = ["EvaluationRunner", "EVAL_USER_ID"]
