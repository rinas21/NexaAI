"""PII detection and input sanitization endpoints."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.core.logging import get_logger
from app.models.schemas import (
    PiiDetectRequest,
    PiiDetectResponse,
    SanitizeResponse,
)
from app.security.guardrails import Guardrails
from app.security.pii import PIIService
from app.services.dependencies import get_guardrails, get_pii_service

router = APIRouter()
logger = get_logger(__name__)


@router.post("/pii/detect", response_model=PiiDetectResponse, summary="Detect (and optionally mask) PII")
async def detect_pii(
    request: PiiDetectRequest,
    service: PIIService = Depends(get_pii_service),
) -> PiiDetectResponse:
    """Detect PII using Presidio when available, otherwise regex fallback."""
    return await service.detect(request.text, language=request.language, mask=request.mask)


@router.post("/sanitize", response_model=SanitizeResponse, summary="Sanitize untrusted text")
async def sanitize(
    request: PiiDetectRequest,
    pii: PIIService = Depends(get_pii_service),
    guardrails: Guardrails = Depends(get_guardrails),
) -> SanitizeResponse:
    """Remove control characters, detect injection attempts and mask PII."""
    result = guardrails.sanitize(request.text)
    sanitized_text = await pii.mask(result.sanitized_text) if request.mask else result.sanitized_text
    if request.mask:
        detected = await pii.detect(request.text)
        threats = list(result.threats_detected)
        threats.extend(f"pii:{entity.entity_type}" for entity in detected.entities)
    else:
        threats = list(result.threats_detected)

    return SanitizeResponse(
        original_text=request.text,
        sanitized_text=sanitized_text,
        is_safe=result.is_safe,
        threats_detected=threats,
    )


__all__ = ["router"]
