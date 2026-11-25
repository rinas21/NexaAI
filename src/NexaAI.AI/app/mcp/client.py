"""Small demo client for the in-process MCP tool server."""

from __future__ import annotations

import asyncio
import json
from typing import Any

from app.core.logging import get_logger
from app.mcp.server import MCPServer
from app.models.schemas import McpToolCallResponse

logger = get_logger(__name__)


class MCPClient:
    """List and call MCP tools exposed by :class:`MCPServer`."""

    def __init__(self, server: MCPServer | None = None) -> None:
        self.server = server or MCPServer()

    async def list_tools(self) -> list[dict[str, Any]]:
        """Return all tool descriptors."""
        return self.server.list_tools()

    async def call_tool(self, name: str, arguments: dict[str, Any] | None = None) -> McpToolCallResponse:
        """Call one tool with the supplied arguments."""
        return await self.server.call_tool(name, arguments or {})


async def demo() -> None:
    """List the tools and invoke the calculator as a smoke test."""
    client = MCPClient()
    print(json.dumps({"tools": await client.list_tools()}, indent=2))
    result = await client.call_tool("calculator", {"expression": "2 + 3 * 4"})
    print(json.dumps(result.model_dump(mode="json"), indent=2))
    logger.info("MCP demo complete", is_error=result.is_error)


if __name__ == "__main__":  # pragma: no cover
    asyncio.run(demo())
