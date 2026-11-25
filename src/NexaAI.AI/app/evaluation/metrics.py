"""Retrieval and generation quality metrics for RAG evaluation."""

from __future__ import annotations

import math
import re
import time
from collections.abc import Iterable, Mapping
from typing import Any

_TOKEN_RE = re.compile(r"[a-z0-9]+")
_STOPWORDS = {
    "a", "an", "and", "are", "as", "at", "be", "by", "do", "does", "for", "from",
    "how", "i", "in", "is", "it", "its", "of", "on", "or", "that", "the", "this",
    "to", "was", "were", "what", "when", "which", "who", "why", "with", "you", "your",
}


def _tokens(text: str, *, drop_stopwords: bool = True) -> set[str]:
    tokens = _TOKEN_RE.findall((text or "").lower())
    if drop_stopwords:
        return {token for token in tokens if token not in _STOPWORDS}
    return set(tokens)


def _relevance(item: str, relevant: Iterable[str] | Mapping[str, Any]) -> float:
    if isinstance(relevant, Mapping):
        return float(relevant.get(item, 0.0))
    return 1.0 if item in set(relevant) else 0.0


def recall_at_k(retrieved: list[str], relevant: Iterable[str] | Mapping[str, Any], k: int = 5) -> float:
    """Fraction of relevant items found in the first ``k`` results."""
    relevant_set = set(relevant)
    if not relevant_set:
        return 0.0
    top = list(retrieved)[:k]
    return len(relevant_set & set(top)) / len(relevant_set)


def mrr(retrieved: list[str], relevant: Iterable[str] | Mapping[str, Any]) -> float:
    """Mean reciprocal rank of the first relevant result (single query)."""
    for rank, item in enumerate(retrieved, start=1):
        if _relevance(item, relevant) > 0:
            return 1.0 / rank
    return 0.0


def ndcg(retrieved: list[str], relevant: Iterable[str] | Mapping[str, Any], k: int = 5) -> float:
    """Normalized discounted cumulative gain for binary or graded relevance."""
    top = list(retrieved)[:k]
    dcg = sum(_relevance(item, relevant) / math.log2(rank + 1) for rank, item in enumerate(top, start=1))
    gains = sorted((_relevance(item, relevant) for item in relevant), reverse=True)[:k]
    ideal = sum(gain / math.log2(rank + 1) for rank, gain in enumerate(gains, start=1))
    return dcg / ideal if ideal > 0 else 0.0


def faithfulness(answer: str, contexts: list[str] | str) -> float:
    """Fraction of answer content words that appear in the grounding context."""
    if isinstance(contexts, str):
        context_text = contexts
    else:
        context_text = " ".join(contexts)
    answer_tokens = _tokens(answer)
    if not answer_tokens:
        return 0.0
    context_tokens = _tokens(context_text)
    return len(answer_tokens & context_tokens) / len(answer_tokens)


def answer_relevance(answer: str, question: str) -> float:
    """Token-overlap relevance between an answer and its question."""
    answer_tokens = _tokens(answer)
    question_tokens = _tokens(question)
    if not answer_tokens or not question_tokens:
        return 0.0
    return len(answer_tokens & question_tokens) / len(question_tokens)


def answer_correctness(answer: str, expected: str) -> float:
    """Token-level F1 score between the generated and expected answer."""
    actual_tokens = _tokens(answer, drop_stopwords=False)
    expected_tokens = _tokens(expected, drop_stopwords=False)
    if not actual_tokens or not expected_tokens:
        return 0.0
    overlap = len(actual_tokens & expected_tokens)
    precision = overlap / len(actual_tokens)
    recall = overlap / len(expected_tokens)
    if precision + recall == 0:
        return 0.0
    return 2 * precision * recall / (precision + recall)


def latency_ms(start_time: float, end_time: float | None = None) -> float:
    """Elapsed time in milliseconds using ``time.perf_counter`` values."""
    end = time.perf_counter() if end_time is None else end_time
    return max(0.0, (end - start_time) * 1000)


__all__ = [
    "answer_correctness",
    "answer_relevance",
    "faithfulness",
    "latency_ms",
    "mrr",
    "ndcg",
    "recall_at_k",
]
