"""HTTP endpoints exposing the MCP tool registry."""

from __future__ import annotations

from typing import Any

from fastapi import APIRouter, Depends

from app.core.logging import get_logger
from app.mcp.server import MCPServer
from app.models.schemas import McpToolCallRequest, McpToolCallResponse
from app.services.dependencies import get_mcp_server

router = APIRouter()
logger = get_logger(__name__)


@router.get("/tools", summary="List MCP tools")
async def list_tools(server: MCPServer = Depends(get_mcp_server)) -> list[dict[str, Any]]:
    """List MCP tools with their JSON schemas."""
    return server.list_tools()


@router.post("/tools/{name}/call", response_model=McpToolCallResponse, summary="Call an MCP tool")
async def call_tool(
    name: str,
    request: McpToolCallRequest,
    server: MCPServer = Depends(get_mcp_server),
) -> McpToolCallResponse:
    """Call one of the MCP tools and report success, result and timing."""
    logger.info("MCP tool call via HTTP", tool=name)
    return await server.call_tool(name, request.arguments)


__all__ = ["router"]
