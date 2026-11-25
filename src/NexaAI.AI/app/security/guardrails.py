"""Prompt-injection, sanitization and output-validation guardrails."""

from __future__ import annotations

import re
from typing import Any

from app.config import settings
from app.core.logging import get_logger
from app.models.schemas import SanitizeResponse

logger = get_logger(__name__)

# (name, pattern, weight). Weights are summed and capped at 1.0 so that a
# combination of weaker signals can cross the configured threshold too.
INJECTION_PATTERNS: list[tuple[str, re.Pattern[str], float]] = [
    (
        "ignore_previous_instructions",
        re.compile(
            r"ignore\s+(?:all\s+)?(?:the\s+)?(?:previous|prior|above|earlier|preceding)"
            r"[\s-]*(?:instructions|prompts|rules|messages|commands)",
            re.I,
        ),
        1.0,
    ),
    (
        "system_prompt_access",
        re.compile(
            r"(?:show|reveal|print|display|repeat|output|dump|share)\s+"
            r"(?:me\s+)?(?:your\s+|the\s+|all\s+)?"
            r"(?:system|initial|hidden|internal)\s+(?:prompt|instructions|message)",
            re.I,
        ),
        1.0,
    ),
    ("system_prompt", re.compile(r"\bsystem\s+prompt\b", re.I), 0.85),
    ("jailbreak", re.compile(r"\bjail\s?break\b", re.I), 0.9),
    ("role_hijack", re.compile(r"\byou\s+are\s+now\b", re.I), 0.9),
    (
        "role_hijack_act_as",
        re.compile(
            r"\bact\s+as\s+(?:an?\s+)?(?:unrestricted|uncensored|developer|evil|"
            r"different|admin|root|dan)\b",
            re.I,
        ),
        0.95,
    ),
    ("role_hijack_pretend", re.compile(r"\bpretend\s+(?:you\s+are|to\s+be)\b", re.I), 0.9),
    (
        "instruction_override",
        re.compile(
            r"\b(?:disregard|forget|override|bypass|ignore)\s+"
            r"(?:all\s+|any\s+|your\s+|the\s+)?"
            r"(?:previous|prior|safety|security|content|your|rules|instructions|guidelines)",
            re.I,
        ),
        0.9,
    ),
    ("developer_mode", re.compile(r"\bdeveloper\s+mode\b", re.I), 0.85),
    (
        "delimiter_injection",
        re.compile(r"<\|(?:im_start|im_end|system|assistant)\|>|\[/?system\]|^###\s*system\b", re.I | re.M),
        0.9,
    ),
    (
        "prompt_leak",
        re.compile(r"\b(?:reveal|print|dump|share)\b.{0,40}\b(?:instructions|rules|prompt)\b", re.I),
        0.8,
    ),
    (
        "do_not_follow",
        re.compile(r"\bdo\s+not\s+follow\s+(?:your|the|any)\s+(?:instructions|rules)\b", re.I),
        0.9,
    ),
    (
        "role_marker",
        re.compile(r"^\s*(?:system|assistant)\s*:\s*", re.I | re.M),
        0.8,
    ),
]

_REFUSAL_PATTERNS = [
    re.compile(pattern, re.I)
    for pattern in (
        r"\bi\s+(?:cannot|can't|am unable to)\s+(?:answer|provide|help|assist)\b",
        r"\bi\s+don't\s+have\s+enough\s+(?:information|context|data)\b",
        r"\bas\s+an\s+ai\s+(?:language\s+)?model,?\s+i\s+(?:cannot|can't)\b",
        r"\bthe\s+(?:answer|information)\s+is\s+not\s+(?:available|in the (?:context|documents))\b",
    )
]

_PII_PATTERNS = {
    "EMAIL": re.compile(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b"),
    "SSN": re.compile(r"\b\d{3}-\d{2}-\d{4}\b"),
    "PHONE": re.compile(r"(?<!\d)(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}(?!\d)"),
}

_TOKEN_RE = re.compile(r"[a-z0-9]+")
_STOPWORDS = {
    "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for",
    "from", "has", "have", "how", "i", "in", "is", "it", "its", "of", "on", "or",
    "that", "the", "their", "there", "this", "to", "was", "were", "what", "when",
    "which", "who", "will", "with", "you", "your",
}
_CITATION_RE = re.compile(r"\[\d+\]")
_CONTROL_RE = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")
_ZERO_WIDTH_RE = re.compile(r"[\u200b-\u200f\u202a-\u202e\ufeff]")


def _tokenize(text: str) -> set[str]:
    return {token for token in _TOKEN_RE.findall(text.lower()) if token not in _STOPWORDS}


class Guardrails:
    """Detect injection attempts and validate model inputs and outputs."""

    def __init__(self, threshold: float | None = None, max_input_length: int | None = None) -> None:
        self.threshold = settings.prompt_injection_threshold if threshold is None else threshold
        self.max_input_length = settings.max_input_length if max_input_length is None else max_input_length

    def risk_score(self, text: str) -> float:
        """Return an injection risk score in ``[0, 1]``."""
        if not text:
            return 0.0
        score = sum(weight for _, pattern, weight in INJECTION_PATTERNS if pattern.search(text))
        return min(1.0, score)

    def detect_threats(self, text: str) -> list[str]:
        """Return the names of all injection patterns found in ``text``."""
        return [name for name, pattern, _ in INJECTION_PATTERNS if pattern.search(text or "")]

    def is_injection(self, text: str) -> bool:
        """Whether the text crosses the configured injection risk threshold."""
        detected = self.detect_threats(text)
        if not detected:
            return False
        if self.risk_score(text) >= self.threshold:
            return True
        return len(detected) >= 2

    def sanitize(self, text: str) -> SanitizeResponse:
        """Clean user input and report any detected injection attempts."""
        original = text or ""
        cleaned = _CONTROL_RE.sub("", original)
        cleaned = _ZERO_WIDTH_RE.sub("", cleaned)
        cleaned = re.sub(r"\n{3,}", "\n\n", cleaned)
        cleaned = re.sub(r"[ \t]{3,}", "  ", cleaned).strip()
        if len(cleaned) > self.max_input_length:
            cleaned = cleaned[: self.max_input_length]
            logger.info("Input truncated to maximum length", limit=self.max_input_length)

        threats = self.detect_threats(cleaned)
        is_safe = not self.is_injection(cleaned)
        if not is_safe:
            logger.warning("Prompt injection detected", threats=threats)
        return SanitizeResponse(
            original_text=original,
            sanitized_text=cleaned,
            is_safe=is_safe,
            threats_detected=threats,
        )

    def validate_output(
        self,
        answer: str,
        context: list[str] | str | list[Any] | None = None,
    ) -> tuple[bool, str | None]:
        """Validate a generated answer.

        Returns ``(is_valid, reason)``. An answer is invalid when it is empty,
        refuses the question, leaks PII, does not overlap the grounding context
        or contains numeric claims absent from that context.
        """
        if not answer or not answer.strip():
            return False, "empty_output"

        for pattern in _REFUSAL_PATTERNS:
            if pattern.search(answer):
                return False, "refusal"

        for kind, pattern in _PII_PATTERNS.items():
            if pattern.search(answer):
                return False, f"pii_leakage:{kind}"

        if context is None:
            return True, None
        if isinstance(context, str):
            context_parts = [context]
        else:
            context_parts = []
            for item in context:
                if isinstance(item, str):
                    context_parts.append(item)
                else:
                    snippet = getattr(item, "content", None) or getattr(item, "snippet", None)
                    if snippet:
                        context_parts.append(str(snippet))

        if not context_parts:
            return True, None

        context_text = "\n".join(context_parts)
        answer_tokens = _tokenize(_CITATION_RE.sub(" ", answer))
        context_tokens = _tokenize(context_text)
        if answer_tokens and len(answer_tokens & context_tokens) / len(answer_tokens) < 0.1:
            return False, "ungrounded"

        answer_numbers = set(re.findall(r"\b\d{2,}\b", _CITATION_RE.sub(" ", answer)))
        context_numbers = set(re.findall(r"\b\d{2,}\b", context_text))
        unsupported = answer_numbers - context_numbers
        if answer_numbers and len(unsupported) / len(answer_numbers) > 0.5:
            return False, "unsupported_claim"

        return True, None


__all__ = ["Guardrails", "INJECTION_PATTERNS"]
