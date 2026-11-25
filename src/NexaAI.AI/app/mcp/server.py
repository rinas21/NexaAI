"""Model Context Protocol tool server with a dependency-free JSON-RPC fallback."""

from __future__ import annotations

import asyncio
import json
import sys
import time
from collections.abc import Callable
from typing import Any

import httpx

from app.agents.tools import calculator, datetime_tool
from app.config import settings
from app.core.exceptions import AIServiceError
from app.core.logging import get_logger
from app.models.schemas import McpToolCallRequest, McpToolCallResponse
from app.services.retrieval import LOCAL_CHUNKS, RetrievalService

logger = get_logger(__name__)

try:  # Optional dependency: MCP Python SDK
    from mcp.server.fastmcp import FastMCP

    MCP_SDK_AVAILABLE = True
except Exception:  # pragma: no cover - depends on the local environment
    MCP_SDK_AVAILABLE = False
    FastMCP = None  # type: ignore[assignment,misc]


async def get_document(document_id: str) -> dict[str, Any]:
    """Fetch a document by id from the C# API, falling back to local chunks."""
    try:
        async with httpx.AsyncClient(
            base_url=settings.api_base_url.rstrip("/"), timeout=10.0
        ) as client:
            response = await client.get(
                f"/api/documents/{document_id}",
                headers={"X-API-Key": settings.api_key},
            )
            response.raise_for_status()
            payload = response.json()
            if isinstance(payload, dict):
                return payload
            return {"document": payload}
    except (httpx.HTTPError, ValueError) as exc:
        logger.warning("Remote document lookup unavailable; checking local store", error=str(exc))

    chunks = [chunk for chunk in LOCAL_CHUNKS if chunk.document_id == str(document_id)]
    if not chunks:
        return {"document_id": document_id, "found": False, "chunks": []}
    return {
        "document_id": document_id,
        "found": True,
        "title": chunks[0].document_title,
        "chunks": [chunk.content for chunk in chunks],
    }


async def list_documents(limit: int = 50) -> list[dict[str, Any]]:
    """List documents from the C# API, falling back to locally stored chunks."""
    try:
        async with httpx.AsyncClient(
            base_url=settings.api_base_url.rstrip("/"), timeout=10.0
        ) as client:
            response = await client.get(
                "/api/documents",
                params={"limit": limit},
                headers={"X-API-Key": settings.api_key},
            )
            response.raise_for_status()
            payload = response.json()
            if isinstance(payload, list):
                return payload[:limit]
            if isinstance(payload, dict) and isinstance(payload.get("documents"), list):
                return payload["documents"][:limit]
    except (httpx.HTTPError, ValueError) as exc:
        logger.warning("Remote document listing unavailable; using local store", error=str(exc))

    documents: dict[str, dict[str, Any]] = {}
    for chunk in LOCAL_CHUNKS:
        documents.setdefault(
            chunk.document_id,
            {"id": chunk.document_id, "title": chunk.document_title, "chunk_count": 0},
        )
        documents[chunk.document_id]["chunk_count"] += 1
    return list(documents.values())[:limit]


class MCPServer:
    """Expose enterprise tools over MCP (or plain JSON-RPC when MCP is absent)."""

    def __init__(self, retrieval_service: RetrievalService | None = None) -> None:
        self.retrieval = retrieval_service
        self.sdk_server: Any | None = None

        async def mcp_document_search(query: str, top_k: int = 5) -> list[dict[str, Any]]:
            """Search indexed enterprise documents for relevant passages."""
            service = self.retrieval or RetrievalService()
            return [
                {
                    "chunk_id": str(result.chunk_id),
                    "document_id": str(result.document_id),
                    "title": result.document_title,
                    "content": result.content,
                    "similarity_score": result.similarity_score,
                }
                for result in await service.search_chunks(query, top_k=top_k)
            ]

        self._tools: dict[str, Callable[..., Any]] = {
            "document_search": mcp_document_search,
            "get_document": get_document,
            "list_documents": list_documents,
            "calculator": calculator,
            "datetime": datetime_tool,
        }
        if MCP_SDK_AVAILABLE and FastMCP is not None:
            try:
                self.sdk_server = FastMCP("nexai-ai")
                for name, func in self._tools.items():
                    self.sdk_server.add_tool(func, name=name)
            except Exception as exc:  # pragma: no cover - SDK version differences
                logger.warning("Could not initialize MCP SDK server", error=str(exc))
                self.sdk_server = None

    def list_tools(self) -> list[dict[str, Any]]:
        """Return MCP-style tool descriptors."""
        descriptions = {
            "document_search": "Search indexed enterprise documents.",
            "get_document": "Fetch a document by id.",
            "list_documents": "List available documents.",
            "calculator": "Safely evaluate an arithmetic expression.",
            "datetime": "Get the current date and time.",
        }
        schemas: dict[str, dict[str, Any]] = {
            "document_search": {
                "type": "object",
                "properties": {
                    "query": {"type": "string"},
                    "top_k": {"type": "integer", "default": 5},
                },
                "required": ["query"],
            },
            "get_document": {
                "type": "object",
                "properties": {"document_id": {"type": "string"}},
                "required": ["document_id"],
            },
            "list_documents": {
                "type": "object",
                "properties": {"limit": {"type": "integer", "default": 50}},
            },
            "calculator": {
                "type": "object",
                "properties": {"expression": {"type": "string"}},
                "required": ["expression"],
            },
            "datetime": {
                "type": "object",
                "properties": {
                    "timezone_name": {"type": "string"},
                    "output_format": {"type": "string"},
                },
            },
        }
        return [
            {
                "name": name,
                "description": descriptions[name],
                "inputSchema": schemas[name],
            }
            for name in self._tools
        ]

    async def call_tool(
        self, name: str | None = None, arguments: dict[str, Any] | None = None, *, request: McpToolCallRequest | None = None
    ) -> McpToolCallResponse:
        """Invoke a tool by name and report errors in MCP response form."""
        started = time.perf_counter()
        if request is not None:
            name, arguments = request.name, request.arguments
        name = name or ""
        func = self._tools.get(name)
        if func is None:
            return McpToolCallResponse(
                result=f"Unknown tool: {name}",
                is_error=True,
                execution_time_ms=(time.perf_counter() - started) * 1000,
            )
        try:
            result = func(**(arguments or {}))
            if asyncio.iscoroutine(result):
                result = await result
        except Exception as exc:
            logger.error("MCP tool call failed", tool=name, error=str(exc))
            return McpToolCallResponse(
                result=exc.message if isinstance(exc, AIServiceError) else str(exc),
                is_error=True,
                execution_time_ms=(time.perf_counter() - started) * 1000,
            )
        return McpToolCallResponse(
            result=result,
            is_error=False,
            execution_time_ms=(time.perf_counter() - started) * 1000,
        )

    async def handle_request(self, request: dict[str, Any]) -> dict[str, Any]:
        """Handle a JSON-RPC request when the MCP SDK is not installed."""
        method = request.get("method")
        request_id = request.get("id")
        params = request.get("params") or {}
        if method == "tools/list":
            result: dict[str, Any] = {"tools": self.list_tools()}
        elif method == "tools/call":
            tool_name = params.get("name")
            arguments = params.get("arguments") or {}
            response = await self.call_tool(tool_name, arguments)
            result = {
                "content": [{"type": "text", "text": json.dumps(response.result, default=str)}],
                "isError": response.is_error,
            }
        else:
            return {
                "jsonrpc": "2.0",
                "id": request_id,
                "error": {"code": -32601, "message": f"Unknown method: {method}"},
            }
        return {"jsonrpc": "2.0", "id": request_id, "result": result}

    async def run_stdio(self) -> None:
        """Serve JSON-RPC over stdin/stdout (SDK stdio when available)."""
        if self.sdk_server is not None:
            await self.sdk_server.run(transport="stdio")
            return
        logger.info("MCP SDK unavailable; using JSON-RPC stdio fallback")
        loop = asyncio.get_running_loop()
        while True:
            line = await loop.run_in_executor(None, sys.stdin.readline)
            if not line:
                break
            try:
                request = json.loads(line)
                response = await self.handle_request(request)
            except json.JSONDecodeError:
                response = {
                    "jsonrpc": "2.0",
                    "id": None,
                    "error": {"code": -32700, "message": "Parse error"},
                }
            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()


mcp_server = MCPServer()


__all__ = ["MCPServer", "MCP_SDK_AVAILABLE", "get_document", "list_documents", "mcp_server"]
