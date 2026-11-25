"""PII detection and masking via Presidio REST with a regex fallback."""

from __future__ import annotations

import re
from typing import Any

import httpx

from app.config import settings
from app.core.exceptions import PIIDetectionError
from app.core.logging import get_logger
from app.models.schemas import PiiDetectResponse, PiiEntity

logger = get_logger(__name__)

# Ordered from most specific to least specific. Offsets are always computed on
# the original text so results can be passed straight to Presidio anonymizer.
FALLBACK_PATTERNS: list[tuple[str, re.Pattern[str], float]] = [
    ("EMAIL_ADDRESS", re.compile(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b"), 0.95),
    ("US_SSN", re.compile(r"\b\d{3}-\d{2}-\d{4}\b"), 0.9),
    ("CREDIT_CARD", re.compile(r"\b(?:\d{4}[ -]?){3}\d{4}\b"), 0.85),
    (
        "PHONE_NUMBER",
        re.compile(r"(?<!\d)(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}(?!\d)"),
        0.75,
    ),
]

REPLACEMENTS = {
    "EMAIL_ADDRESS": "[EMAIL]",
    "US_SSN": "[SSN]",
    "CREDIT_CARD": "[CREDIT_CARD]",
    "PHONE_NUMBER": "[PHONE]",
}


def _luhn_valid(number: str) -> bool:
    digits = [int(char) for char in re.sub(r"\D", "", number)]
    if not 13 <= len(digits) <= 19:
        return False
    total = 0
    for index, digit in enumerate(reversed(digits)):
        if index % 2 == 1:
            digit *= 2
            if digit > 9:
                digit -= 9
        total += digit
    return total % 10 == 0


class PIIService:
    """Detect and mask PII, preferring a running Presidio deployment."""

    def __init__(
        self,
        enabled: bool | None = None,
        analyzer_url: str | None = None,
        anonymizer_url: str | None = None,
        language: str | None = None,
        min_score: float | None = None,
        *,
        timeout_seconds: float = 2.0,
    ) -> None:
        self.enabled = settings.presidio_enabled if enabled is None else enabled
        self.analyzer_url = (analyzer_url or settings.presidio_analyzer_url).rstrip("/")
        self.anonymizer_url = (anonymizer_url or settings.presidio_anonymizer_url).rstrip("/")
        self.language = language or settings.presidio_language
        self.min_score = settings.presidio_min_score if min_score is None else min_score
        self.timeout_seconds = timeout_seconds
        self.presidio_available = False

    async def _presidio_analyze(self, text: str, language: str) -> list[PiiEntity] | None:
        """Call the Presidio analyzer REST endpoint, or ``None`` on failure."""
        if not self.enabled:
            return None
        try:
            async with httpx.AsyncClient(timeout=self.timeout_seconds) as client:
                response = await client.post(
                    f"{self.analyzer_url}/analyze",
                    json={"text": text, "language": language, "return_decision_process": False},
                )
                response.raise_for_status()
                payload = response.json()
        except (httpx.HTTPError, ValueError) as exc:
            logger.warning("Presidio analyzer unavailable; using regex fallback", error=str(exc))
            return None

        if not isinstance(payload, list):
            return None
        entities = [
            PiiEntity(
                entity_type=str(item.get("entity_type", "UNKNOWN")),
                start=int(item.get("start", 0)),
                end=int(item.get("end", 0)),
                score=float(item.get("score", 0.0)),
                text=text[int(item.get("start", 0)) : int(item.get("end", 0))],
            )
            for item in payload
            if isinstance(item, dict) and float(item.get("score", 0.0)) >= self.min_score
        ]
        self.presidio_available = True
        return entities

    async def _presidio_anonymize(self, text: str, entities: list[PiiEntity]) -> str | None:
        """Call the Presidio anonymizer REST endpoint, or ``None`` on failure."""
        if not self.enabled:
            return None
        analyzer_results = [
            {
                "entity_type": entity.entity_type,
                "start": entity.start,
                "end": entity.end,
                "score": entity.score,
            }
            for entity in entities
        ]
        try:
            async with httpx.AsyncClient(timeout=self.timeout_seconds) as client:
                response = await client.post(
                    f"{self.anonymizer_url}/anonymize",
                    json={"text": text, "analyzer_results": analyzer_results},
                )
                response.raise_for_status()
                payload = response.json()
        except (httpx.HTTPError, ValueError) as exc:
            logger.warning("Presidio anonymizer unavailable; using local masking", error=str(exc))
            return None
        result = payload.get("text") if isinstance(payload, dict) else None
        return str(result) if result is not None else None

    def detect_fallback(self, text: str) -> list[PiiEntity]:
        """Regex-based PII detection that requires no external services."""
        entities: list[PiiEntity] = []
        occupied: list[tuple[int, int]] = []
        for entity_type, pattern, score in FALLBACK_PATTERNS:
            for match in pattern.finditer(text):
                start, end = match.span()
                if entity_type == "CREDIT_CARD" and not _luhn_valid(match.group(0)):
                    continue
                if any(start < taken_end and end > taken_start for taken_start, taken_end in occupied):
                    continue
                occupied.append((start, end))
                entities.append(
                    PiiEntity(
                        entity_type=entity_type,
                        start=start,
                        end=end,
                        score=score,
                        text=match.group(0),
                    )
                )
        entities.sort(key=lambda entity: entity.start)
        return entities

    async def detect(self, text: str, language: str | None = None, mask: bool = False) -> PiiDetectResponse:
        """Detect PII in ``text`` and optionally return the masked version."""
        language = language or self.language
        entities = await self._presidio_analyze(text, language)
        if entities is None:
            entities = self.detect_fallback(text)

        processed = await self.mask(text, entities) if mask else text
        return PiiDetectResponse(
            original_text=text,
            processed_text=processed,
            entities=entities,
            has_pii=bool(entities),
        )

    async def mask(self, text: str, entities: list[PiiEntity] | None = None) -> str:
        """Replace detected PII with typed placeholder tokens."""
        if entities is None:
            presidio_entities = await self._presidio_analyze(text, self.language)
            entities = presidio_entities if presidio_entities is not None else self.detect_fallback(text)
        if not entities:
            return text

        anonymized = await self._presidio_anonymize(text, entities)
        if anonymized is not None:
            return anonymized

        result = text
        for entity in sorted(entities, key=lambda item: item.start, reverse=True):
            replacement = REPLACEMENTS.get(entity.entity_type, f"[{entity.entity_type}]")
            result = result[: entity.start] + replacement + result[entity.end :]
        return result

    async def sanitize(self, text: str, language: str | None = None) -> PiiDetectResponse:
        """Alias for :meth:`detect` with ``mask=True``."""
        return await self.detect(text, language=language, mask=True)


def create_pii_service(**kwargs: Any) -> PIIService:
    """Factory kept for convenient dependency overrides in tests."""
    try:
        return PIIService(**kwargs)
    except Exception as exc:  # pragma: no cover - defensive configuration guard
        raise PIIDetectionError(f"Failed to initialize PII service: {exc}") from exc


__all__ = ["PIIService", "FALLBACK_PATTERNS", "REPLACEMENTS", "create_pii_service"]
