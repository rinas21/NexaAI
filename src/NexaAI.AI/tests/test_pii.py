"""Unit tests for the dependency-free PII fallback."""

import pytest

from app.security.pii import PIIService

pytestmark = pytest.mark.unit


SAMPLE = (
    "Email alice@example.com or call 555-123-4567. "
    "SSN 123-45-6789. Card 4111 1111 1111 1111."
)


async def test_fallback_detects_common_pii_types() -> None:
    service = PIIService(enabled=False)
    result = await service.detect(SAMPLE)
    types = {entity.entity_type for entity in result.entities}

    assert result.has_pii
    assert "EMAIL_ADDRESS" in types
    assert "PHONE_NUMBER" in types
    assert "US_SSN" in types
    assert "CREDIT_CARD" in types


async def test_fallback_mask_replaces_detected_values() -> None:
    service = PIIService(enabled=False)
    masked = await service.mask(SAMPLE)

    assert "alice@example.com" not in masked
    assert "123-45-6789" not in masked
    assert "[EMAIL]" in masked
    assert "[SSN]" in masked
    assert "[CREDIT_CARD]" in masked
    assert "[PHONE]" in masked


async def test_no_pii_returns_original_text() -> None:
    service = PIIService(enabled=False)
    result = await service.detect("The quarterly report is ready.", mask=True)

    assert not result.has_pii
    assert result.processed_text == "The quarterly report is ready."
