"""Agent tool implementations with an optional Semantic Kernel integration.

Every tool is also a plain Python callable so the service remains importable
and testable when ``semantic-kernel`` is not installed.
"""

from __future__ import annotations

import ast
import inspect
import math
import operator
import re
import time
from collections.abc import Callable
from datetime import datetime, timezone
from functools import wraps
from typing import Any
from uuid import UUID, uuid4
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

import httpx

from app.config import settings
from app.core.exceptions import ApprovalRequiredError, ToolExecutionError
from app.core.logging import get_logger
from app.models.schemas import ToolInfo
from app.services.retrieval import RetrievalService

logger = get_logger(__name__)

try:  # Optional dependency: Semantic Kernel
    from semantic_kernel.functions import kernel_function

    SK_AVAILABLE = True
except Exception:  # pragma: no cover - depends on the local environment
    SK_AVAILABLE = False

    def kernel_function(*, name: str | None = None, description: str | None = None, **_: Any):
        """No-op decorator used when Semantic Kernel is unavailable."""

        def decorator(func: Callable[..., Any]) -> Callable[..., Any]:
            @wraps(func)
            def wrapper(*args: Any, **kwargs: Any) -> Any:
                return func(*args, **kwargs)

            wrapper._tool_name = name or func.__name__
            wrapper._tool_description = description or (func.__doc__ or "").strip()
            return wrapper

        return decorator


# ---------------------------------------------------------------------------
# Calculator: safe AST evaluation (never calls eval/exec)
# ---------------------------------------------------------------------------

_BIN_OPS: dict[type[ast.operator], Callable[[Any, Any], Any]] = {
    ast.Add: operator.add,
    ast.Sub: operator.sub,
    ast.Mult: operator.mul,
    ast.Div: operator.truediv,
    ast.FloorDiv: operator.floordiv,
    ast.Mod: operator.mod,
    ast.Pow: operator.pow,
}
_UNARY_OPS: dict[type[ast.unaryop], Callable[[Any], Any]] = {
    ast.UAdd: operator.pos,
    ast.USub: operator.neg,
}
_FUNCTIONS: dict[str, Callable[..., Any]] = {
    "abs": abs,
    "round": round,
    "min": min,
    "max": max,
    "pow": pow,
    "sqrt": math.sqrt,
    "log": math.log,
    "log10": math.log10,
    "exp": math.exp,
    "floor": math.floor,
    "ceil": math.ceil,
    "sin": math.sin,
    "cos": math.cos,
    "tan": math.tan,
}
_CONSTANTS: dict[str, float] = {"pi": math.pi, "e": math.e}
_MAX_EXPRESSION_LENGTH = 500
_MAX_POWER = 1000


def _eval_node(node: ast.AST) -> Any:
    if isinstance(node, ast.Expression):
        return _eval_node(node.body)
    if isinstance(node, ast.Constant) and isinstance(node.value, (int, float)) and not isinstance(node.value, bool):
        return node.value
    if isinstance(node, ast.BinOp) and type(node.op) in _BIN_OPS:
        left = _eval_node(node.left)
        right = _eval_node(node.right)
        if isinstance(node.op, ast.Pow) and abs(right) > _MAX_POWER:
            raise ToolExecutionError("calculator", "Exponent is too large")
        return _BIN_OPS[type(node.op)](left, right)
    if isinstance(node, ast.UnaryOp) and type(node.op) in _UNARY_OPS:
        return _UNARY_OPS[type(node.op)](_eval_node(node.operand))
    if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in _FUNCTIONS:
        if node.keywords:
            raise ToolExecutionError("calculator", "Keyword arguments are not allowed")
        return _FUNCTIONS[node.func.id](*[_eval_node(arg) for arg in node.args])
    if isinstance(node, ast.Name) and node.id in _CONSTANTS:
        return _CONSTANTS[node.id]
    raise ToolExecutionError("calculator", f"Unsupported expression element: {type(node).__name__}")


def calculator(expression: str) -> dict[str, Any]:
    """Safely evaluate an arithmetic expression using a restricted AST walker."""
    if not isinstance(expression, str) or not expression.strip():
        raise ToolExecutionError("calculator", "Expression must be a non-empty string")
    if len(expression) > _MAX_EXPRESSION_LENGTH:
        raise ToolExecutionError("calculator", "Expression is too long")
    try:
        tree = ast.parse(expression, mode="eval")
        result = _eval_node(tree)
    except ToolExecutionError:
        raise
    except (SyntaxError, ValueError, TypeError, ZeroDivisionError, OverflowError) as exc:
        raise ToolExecutionError("calculator", str(exc)) from exc
    if isinstance(result, complex) or (isinstance(result, float) and not math.isfinite(result)):
        raise ToolExecutionError("calculator", "Result is not a finite number")
    return {"expression": expression, "result": result}


# ---------------------------------------------------------------------------
# Date/time tool
# ---------------------------------------------------------------------------

def datetime_tool(timezone_name: str | None = None, output_format: str | None = None) -> dict[str, Any]:
    """Return the current time, optionally in a named IANA timezone."""
    try:
        tz = ZoneInfo(timezone_name) if timezone_name else timezone.utc
    except (ZoneInfoNotFoundError, ValueError) as exc:
        raise ToolExecutionError("datetime", f"Unknown timezone: {timezone_name}") from exc
    now = datetime.now(tz)
    return {
        "datetime": now.isoformat(),
        "timezone": timezone_name or "UTC",
        "unix_timestamp": now.timestamp(),
        "formatted": now.strftime(output_format) if output_format else now.isoformat(),
    }


# ---------------------------------------------------------------------------
# Document search tool
# ---------------------------------------------------------------------------

async def document_search(
    query: str,
    top_k: int = 5,
    user_id: str | None = None,
    access_level: str | None = None,
    retrieval_service: RetrievalService | None = None,
) -> list[dict[str, Any]]:
    """Search indexed documents through the retrieval service."""
    if retrieval_service is None:
        from app.services.dependencies import get_ollama_client

        retrieval_service = RetrievalService(ollama_client=get_ollama_client())

    results = await retrieval_service.search_chunks(
        query,
        top_k=top_k,
        user_id=UUID(str(user_id)) if user_id else None,
        access_level=access_level,
    )
    return [
        {
            "chunk_id": str(result.chunk_id),
            "document_id": str(result.document_id),
            "document_title": result.document_title,
            "content": result.content,
            "similarity_score": result.similarity_score,
        }
        for result in results
    ]


# ---------------------------------------------------------------------------
# Database tool (read-only, parameterized, proxied through the C# API)
# ---------------------------------------------------------------------------

_READ_ONLY_RE = re.compile(r"^\s*(?:select|with|explain|show)\b", re.I)
_FORBIDDEN_RE = re.compile(
    r"\b(?:insert|update|delete|drop|alter|create|truncate|grant|revoke|execute|exec|"
    r"call|merge|replace|vacuum|attach|detach|copy)\b",
    re.I,
)


def validate_read_only_sql(query: str) -> str:
    """Reject anything that is not a single read-only statement."""
    if not isinstance(query, str) or not query.strip():
        raise ToolExecutionError("database_query", "Query must be a non-empty string")
    if ";" in query.rstrip(";").rstrip():
        raise ToolExecutionError("database_query", "Multiple statements are not allowed")
    if not _READ_ONLY_RE.match(query):
        raise ToolExecutionError("database_query", "Only read-only SELECT queries are allowed")
    if _FORBIDDEN_RE.search(query):
        raise ToolExecutionError("database_query", "Query contains a forbidden keyword")
    return query.strip().rstrip(";")


async def database_query(
    query: str,
    parameters: dict[str, Any] | None = None,
    approved: bool = False,
) -> dict[str, Any]:
    """Run a parameterized read-only SQL query through the ASP.NET API."""
    if not approved:
        raise ApprovalRequiredError("database_query", f"apr-{uuid4().hex}")
    safe_query = validate_read_only_sql(query)
    try:
        async with httpx.AsyncClient(
            base_url=settings.api_base_url.rstrip("/"), timeout=10.0
        ) as client:
            response = await client.post(
                "/api/database/query",
                json={"sql": safe_query, "parameters": parameters or {}},
                headers={"X-API-Key": settings.api_key},
            )
            response.raise_for_status()
            payload = response.json()
    except (httpx.HTTPError, ValueError) as exc:
        raise ToolExecutionError("database_query", str(exc)) from exc
    return {"columns": payload.get("columns", []), "rows": payload.get("rows", [])} if isinstance(payload, dict) else {"rows": payload}


async def delete_document(document_id: str, approved: bool = False) -> dict[str, Any]:
    """Delete a document through the management API (requires approval)."""
    if not approved:
        raise ApprovalRequiredError("delete_document", f"apr-{uuid4().hex}")
    try:
        async with httpx.AsyncClient(
            base_url=settings.api_base_url.rstrip("/"), timeout=10.0
        ) as client:
            response = await client.delete(
                f"/api/documents/{document_id}",
                headers={"X-API-Key": settings.api_key},
            )
            response.raise_for_status()
    except httpx.HTTPError as exc:
        raise ToolExecutionError("delete_document", str(exc)) from exc
    return {"document_id": document_id, "deleted": True}


# ---------------------------------------------------------------------------
# Semantic Kernel plugins (plain callables are registered when SK is absent)
# ---------------------------------------------------------------------------

class CalculatorPlugin:
    """SK plugin wrapper for arithmetic."""

    @kernel_function(name="calculator", description="Evaluate an arithmetic expression safely.")
    def calculator(self, expression: str) -> dict[str, Any]:
        """Evaluate an arithmetic expression safely."""
        return calculator(expression)


class DateTimePlugin:
    """SK plugin wrapper for the current date and time."""

    @kernel_function(name="datetime", description="Get the current date and time.")
    def datetime(self, timezone_name: str | None = None, output_format: str | None = None) -> dict[str, Any]:
        """Get the current date and time."""
        return datetime_tool(timezone_name, output_format)


class DocumentSearchPlugin:
    """SK plugin wrapper for document retrieval."""

    def __init__(self, retrieval_service: RetrievalService | None = None) -> None:
        self.retrieval_service = retrieval_service

    @kernel_function(name="document_search", description="Search enterprise documents for relevant passages.")
    async def document_search(self, query: str, top_k: int = 5) -> list[dict[str, Any]]:
        """Search enterprise documents for relevant passages."""
        return await document_search(query, top_k, retrieval_service=self.retrieval_service)


class DatabasePlugin:
    """SK plugin wrapper for read-only SQL queries."""

    @kernel_function(name="database_query", description="Run a read-only parameterized SQL query.")
    async def database_query(self, query: str, parameters: dict[str, Any] | None = None) -> dict[str, Any]:
        """Run a read-only parameterized SQL query."""
        return await database_query(query, parameters)


class ToolRegistry:
    """Minimal tool registry used as the fallback when SK is unavailable."""

    def __init__(self) -> None:
        self._tools: dict[str, dict[str, Any]] = {}

    def register(
        self,
        name: str,
        func: Callable[..., Any],
        *,
        description: str = "",
        parameters: dict[str, Any] | None = None,
        requires_approval: bool = False,
    ) -> None:
        """Register a callable under ``name``."""
        self._tools[name] = {
            "func": func,
            "description": description or (func.__doc__ or "").strip(),
            "parameters": parameters or {},
            "requires_approval": requires_approval,
        }

    def get(self, name: str) -> Callable[..., Any]:
        """Return a registered tool callable."""
        if name not in self._tools:
            raise ToolExecutionError(name, "Unknown tool")
        return self._tools[name]["func"]

    def has(self, name: str) -> bool:
        """Whether a tool is registered."""
        return name in self._tools

    def list_tools(self) -> list[ToolInfo]:
        """Describe all registered tools."""
        return [
            ToolInfo(
                name=name,
                description=entry["description"],
                parameters=entry["parameters"],
                requires_approval=entry["requires_approval"],
            )
            for name, entry in self._tools.items()
        ]

    async def call(self, name: str, arguments: dict[str, Any] | None = None, **kwargs: Any) -> Any:
        """Execute a tool, awaiting coroutine functions when necessary."""
        if name not in self._tools:
            raise ToolExecutionError(name, "Unknown tool")
        args = {**(arguments or {}), **kwargs}
        func = self._tools[name]["func"]
        started = time.perf_counter()
        try:
            result = func(**args) if args else func()
            if inspect.isawaitable(result):
                result = await result
        except ToolExecutionError:
            raise
        except Exception as exc:
            raise ToolExecutionError(name, str(exc)) from exc
        logger.info(
            "Tool executed",
            tool=name,
            latency_ms=(time.perf_counter() - started) * 1000,
        )
        return result


def create_default_registry(retrieval_service: RetrievalService | None = None) -> ToolRegistry:
    """Build the registry with all built-in enterprise tools."""
    registry = ToolRegistry()
    registry.register(
        "calculator",
        calculator,
        description="Evaluate an arithmetic expression safely.",
        parameters={"expression": {"type": "string", "description": "Arithmetic expression"}},
    )
    registry.register(
        "datetime",
        datetime_tool,
        description="Get the current date and time.",
        parameters={
            "timezone_name": {"type": "string", "description": "IANA timezone name"},
            "output_format": {"type": "string", "description": "Optional strftime format"},
        },
    )
    registry.register(
        "document_search",
        lambda query, top_k=5, user_id=None, access_level=None: document_search(
            query, top_k, user_id, access_level, retrieval_service
        ),
        description="Search enterprise documents for relevant passages.",
        parameters={"query": {"type": "string", "description": "Search query"}},
    )
    registry.register(
        "database_query",
        database_query,
        description="Run a read-only parameterized SQL query through the management API.",
        parameters={"query": {"type": "string", "description": "Read-only SQL SELECT"}},
        requires_approval=True,
    )
    registry.register(
        "delete_document",
        delete_document,
        description="Delete a document through the management API.",
        parameters={"document_id": {"type": "string", "description": "Document identifier"}},
        requires_approval=True,
    )
    return registry


__all__ = [
    "SK_AVAILABLE",
    "ToolRegistry",
    "CalculatorPlugin",
    "DateTimePlugin",
    "DocumentSearchPlugin",
    "DatabasePlugin",
    "calculator",
    "datetime_tool",
    "document_search",
    "database_query",
    "delete_document",
    "create_default_registry",
]
