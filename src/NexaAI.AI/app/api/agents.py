"""Agent execution and tool discovery endpoints."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.agents.agent import AgentService
from app.core.logging import get_logger
from app.models.schemas import AgentRequest, AgentResponse, ToolInfo
from app.services.dependencies import get_agent_service

router = APIRouter()
logger = get_logger(__name__)


@router.post("/execute", response_model=AgentResponse, summary="Run the agent")
async def execute(
    request: AgentRequest,
    service: AgentService = Depends(get_agent_service),
) -> AgentResponse:
    """Run a bounded tool-calling loop; sensitive tools require approval.

    When approval is requested, an ``ApprovalRequiredError`` is raised and the
    global error handler responds with HTTP 202 and the ``approval_id``.
    """
    logger.info("Agent execution request", max_iterations=request.max_iterations)
    return await service.execute(request)


@router.get("/tools", response_model=list[ToolInfo], summary="List agent tools")
async def list_tools(
    service: AgentService = Depends(get_agent_service),
) -> list[ToolInfo]:
    """List tools available to the agent and which ones need approval."""
    return service.tool_registry.list_tools()


__all__ = ["router"]
