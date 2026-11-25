"""Shared FastAPI dependency providers for AI service instances."""

from fastapi import Depends

from app.agents.agent import AgentService
from app.config import settings
from app.core.exceptions import OllamaError
from app.evaluation.runner import EvaluationRunner
from app.mcp.server import MCPServer
from app.security.guardrails import Guardrails
from app.security.pii import PIIService
from app.services.ollama_client import OllamaClient
from app.services.rag import RagService
from app.services.retrieval import RetrievalService


def get_ollama_client() -> OllamaClient:
    """Return the Ollama client created during application startup.

    ``app.main`` is imported locally: it imports routers, which in turn import
    this module. A module-level import would create a cycle and would also
    capture ``ollama_client`` before the lifespan sets it.
    """
    from app import main

    client = getattr(main, "ollama_client", None)
    if client is None:
        raise OllamaError("Ollama client is not initialized")
    return client


def get_retrieval_service(client: OllamaClient = Depends(get_ollama_client)) -> RetrievalService:
    """Build a retrieval service bound to the shared Ollama client."""
    return RetrievalService(ollama_client=client, api_base_url=settings.api_base_url)


def get_rag_service(
    client: OllamaClient = Depends(get_ollama_client),
    retrieval: RetrievalService = Depends(get_retrieval_service),
) -> RagService:
    """Build the RAG pipeline service."""
    return RagService(ollama_client=client, retrieval_service=retrieval)


def get_agent_service(
    client: OllamaClient = Depends(get_ollama_client),
    retrieval: RetrievalService = Depends(get_retrieval_service),
) -> AgentService:
    """Build the Semantic Kernel backed agent service."""
    from app.agents.tools import create_default_registry

    return AgentService(
        ollama_client=client,
        tool_registry=create_default_registry(retrieval_service=retrieval),
    )


def get_pii_service() -> PIIService:
    """Build the PII detection service (Presidio with regex fallback)."""
    return PIIService()


def get_guardrails() -> Guardrails:
    """Build the guardrails service."""
    return Guardrails()


def get_evaluation_runner(
    rag: RagService = Depends(get_rag_service),
    retrieval: RetrievalService = Depends(get_retrieval_service),
) -> EvaluationRunner:
    """Build the evaluation runner."""
    return EvaluationRunner(rag_service=rag, retrieval_service=retrieval)


def get_mcp_server(retrieval: RetrievalService = Depends(get_retrieval_service)) -> MCPServer:
    """Build the MCP tool server."""
    return MCPServer(retrieval_service=retrieval)
