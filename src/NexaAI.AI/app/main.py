"""FastAPI application entry point for the Enterprise AI Assistant AI service."""

from contextlib import asynccontextmanager
from datetime import datetime

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.config import settings
from app.core.exceptions import AIServiceError
from app.core.logging import configure_logging, get_logger
from app.models.schemas import HealthResponse, HealthStatus
from app.services.ollama_client import OllamaClient

configure_logging()
logger = get_logger(__name__)

# Global service instances (initialized in lifespan)
ollama_client: OllamaClient | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage application lifecycle."""
    global ollama_client
    logger.info("Starting AI Service", version=settings.app_version)

    ollama_client = OllamaClient()
    try:
        models = await ollama_client.list_models()
        logger.info("Ollama connected", models=models)
    except Exception as exc:
        logger.warning("Ollama not available at startup", error=str(exc))

    yield

    logger.info("Shutting down AI Service")
    if ollama_client:
        await ollama_client.close()


app = FastAPI(
    title=settings.app_name,
    version=settings.app_version,
    description=(
        "AI service for Enterprise AI Assistant: RAG, Semantic Kernel agents, "
        "MCP tools, Presidio PII protection, and evaluation. "
        "All inference runs locally via Ollama."
    ),
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_url="/openapi.json",
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://localhost:5173", "http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# =============================================================================
# Exception Handlers
# =============================================================================

@app.exception_handler(AIServiceError)
async def ai_service_error_handler(request: Request, exc: AIServiceError) -> JSONResponse:
    """Handle all custom AI service exceptions."""
    logger.error(
        "AI service error",
        error_code=exc.error_code,
        message=exc.message,
        path=request.url.path,
        details=exc.details,
    )
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": exc.error_code,
            "error_code": exc.error_code,
            "message": exc.message,
            "details": exc.details,
            "timestamp": datetime.utcnow().isoformat(),
        },
    )


@app.exception_handler(Exception)
async def generic_error_handler(request: Request, exc: Exception) -> JSONResponse:
    """Handle unexpected errors."""
    logger.exception("Unhandled exception", path=request.url.path, error=str(exc))
    return JSONResponse(
        status_code=500,
        content={
            "error": "INTERNAL_ERROR",
            "error_code": "INTERNAL_ERROR",
            "message": "An unexpected error occurred",
            "details": {},
            "timestamp": datetime.utcnow().isoformat(),
        },
    )


# =============================================================================
# Health & Root
# =============================================================================

@app.get("/", tags=["System"])
async def root() -> dict:
    """Root endpoint with service information."""
    return {
        "service": settings.app_name,
        "version": settings.app_version,
        "docs": "/docs",
        "health": "/health",
    }


@app.get("/health", response_model=HealthResponse, tags=["System"])
async def health() -> HealthResponse:
    """Basic health check endpoint."""
    services: dict[str, str] = {}

    if ollama_client:
        try:
            await ollama_client.is_healthy()
            services["ollama"] = "healthy"
        except Exception:
            services["ollama"] = "unhealthy"

    overall = HealthStatus.HEALTHY
    if any(status == "unhealthy" for status in services.values()):
        overall = HealthStatus.DEGRADED

    return HealthResponse(
        status=overall,
        version=settings.app_version,
        services=services,
    )


@app.get("/health/detailed", tags=["System"])
async def detailed_health() -> dict:
    """Detailed health check with service versions and configuration."""
    models: list[str] = []
    if ollama_client:
        try:
            models = await ollama_client.list_models()
        except Exception:
            pass

    return {
        "status": "healthy",
        "version": settings.app_version,
        "configuration": {
            "llm_model": settings.ollama_llm_model,
            "embedding_model": settings.ollama_embedding_model,
            "presidio_enabled": settings.presidio_enabled,
            "mcp_enabled": settings.feature_enable_mcp,
            "evaluation_enabled": settings.feature_enable_evaluation,
        },
        "ollama_models": models,
        "timestamp": datetime.utcnow().isoformat(),
    }


# =============================================================================
# Include Routers
# =============================================================================

from app.api import agents, embeddings, evaluation, mcp, rag, retrieval, security

app.include_router(embeddings.router, prefix="/api/v1/embeddings", tags=["Embeddings"])
app.include_router(retrieval.router, prefix="/api/v1/search", tags=["Retrieval"])
app.include_router(rag.router, prefix="/api/v1/rag", tags=["RAG"])
app.include_router(agents.router, prefix="/api/v1/agents", tags=["Agents"])
app.include_router(security.router, prefix="/api/v1/security", tags=["Security"])
app.include_router(evaluation.router, prefix="/api/v1/evaluation", tags=["Evaluation"])
app.include_router(mcp.router, prefix="/mcp", tags=["MCP"])