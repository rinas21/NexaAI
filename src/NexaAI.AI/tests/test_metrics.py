"""Unit tests for retrieval and generation metrics."""

import pytest

from app.evaluation.metrics import (
    answer_correctness,
    answer_relevance,
    faithfulness,
    mrr,
    ndcg,
    recall_at_k,
)

pytestmark = pytest.mark.unit


def test_retrieval_metrics() -> None:
    retrieved = ["doc-c", "doc-a", "doc-x", "doc-b"]
    relevant = ["doc-a", "doc-b"]

    assert recall_at_k(retrieved, relevant, k=2) == 0.5
    assert recall_at_k(retrieved, relevant, k=4) == 1.0
    assert mrr(retrieved, relevant) == 0.5
    assert ndcg(retrieved, relevant, k=1) == 0.0
    assert ndcg(retrieved, relevant, k=4) > 0.0
    assert recall_at_k(retrieved, [], k=4) == 0.0


def test_generation_metrics() -> None:
    context = "France's capital city is Paris."
    answer = "Paris is the capital of France."

    assert faithfulness(answer, [context]) == 1.0
    assert faithfulness("The moon is made of cheese", [context]) == 0.0
    assert answer_relevance(answer, "What is the capital of France?") > 0.0
    assert answer_correctness(answer, "The capital of France is Paris") == 1.0
    assert answer_correctness("Completely different text", "Expected answer") == 0.0
