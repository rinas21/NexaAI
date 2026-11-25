"""Semantic Kernel style agent service with an iterative tool-calling loop."""

from __future__ import annotations

import json
import re
import time
from typing import Any
from uuid import uuid4

from app.agents.tools import SK_AVAILABLE, ToolRegistry, create_default_registry
from app.core.exceptions import ApprovalRequiredError, ToolExecutionError
from app.core.logging import get_logger
from app.models.schemas import (
    AgentRequest,
    AgentResponse,
    TokenUsage,
    ToolCall,
    ToolInfo,
)
from app.services.ollama_client import OllamaClient
from app.services.retrieval import RetrievalService

logger = get_logger(__name__)

try:
    from semantic_kernel import Kernel
except Exception:  # pragma: no cover - depends on the local environment
    SK_AVAILABLE = False

    class Kernel:  # type: ignore[no-redef]
        """Tiny stand-in exposing only the API used by this service."""

        def __init__(self) -> None:
            self.plugins: dict[str, Any] = {}

        def add_plugin(self, plugin: Any, plugin_name: str | None = None) -> None:
            self.plugins[plugin_name or type(plugin).__name__] = plugin


_JSON_BLOCK_RE = re.compile(r"```(?:json)?\s*(\{.*?\})\s*```", re.I | re.S)


def _parse_tool_call(text: str) -> dict[str, Any] | None:
    """Parse a JSON tool call or final answer from a model response."""
    if not text:
        return None
    candidates: list[str] = [text.strip()]
    block = _JSON_BLOCK_RE.search(text)
    if block:
        candidates.insert(0, block.group(1))
    start, end = text.find("{"), text.rfind("}")
    if start != -1 and end > start:
        candidates.append(text[start : end + 1])

    for candidate in candidates:
        try:
            payload = json.loads(candidate)
        except (json.JSONDecodeError, TypeError):
            continue
        if not isinstance(payload, dict):
            continue
        tool_name = payload.get("tool") or payload.get("tool_name") or payload.get("name")
        arguments = payload.get("arguments") or payload.get("args") or payload.get("parameters") or {}
        answer = payload.get("answer") or payload.get("output") or payload.get("response")
        if tool_name and isinstance(arguments, dict):
            return {"tool": str(tool_name), "arguments": arguments}
        if answer is not None:
            return {"answer": str(answer)}
    return None


def _usage_from(data: dict[str, Any]) -> TokenUsage:
    def as_int(value: Any) -> int:
        try:
            return int(value or 0)
        except (TypeError, ValueError):
            return 0

    prompt = as_int(data.get("prompt_eval_count"))
    completion = as_int(data.get("eval_count"))
    return TokenUsage(
        prompt_tokens=prompt,
        completion_tokens=completion,
        total_tokens=prompt + completion,
    )


class AgentService:
    """Run a local LLM through a bounded tool-calling loop."""

    def __init__(
        self,
        ollama_client: OllamaClient | None = None,
        tool_registry: ToolRegistry | None = None,
    ) -> None:
        self.ollama = ollama_client or OllamaClient()
        self.tool_registry = tool_registry or create_default_registry()
        self.kernel = Kernel()
        if SK_AVAILABLE:
            self._register_kernel_plugins()

    def _register_kernel_plugins(self) -> None:
        """Expose the built-in tools as SK plugins when SK is installed."""
        from app.agents.tools import (
            CalculatorPlugin,
            DatabasePlugin,
            DateTimePlugin,
            DocumentSearchPlugin,
        )

        plugins = [
            ("Calculator", CalculatorPlugin()),
            ("Time", DateTimePlugin()),
            ("Documents", DocumentSearchPlugin()),
            ("Database", DatabasePlugin()),
        ]
        for name, plugin in plugins:
            try:
                self.kernel.add_plugin(plugin, plugin_name=name)
            except Exception as exc:  # pragma: no cover - SK API version differences
                logger.warning("Could not register SK plugin", plugin=name, error=str(exc))

    def _available_tools(self, request: AgentRequest) -> list[ToolInfo]:
        infos = self.tool_registry.list_tools()
        if request.tools is None:
            return infos
        selected = set(request.tools)
        return [info for info in infos if info.name in selected]

    @staticmethod
    def _system_prompt(tools: list[ToolInfo]) -> str:
        descriptions = "\n".join(
            f"- {tool.name}: {tool.description} | parameters: {json.dumps(tool.parameters)}"
            for tool in tools
        )
        return (
            "You are an enterprise assistant with access to the following tools:\n"
            f"{descriptions or '(none)'}\n\n"
            "Respond with ONLY a JSON object. To use one tool, reply exactly: "
            '{"tool": "tool_name", "arguments": {"key": "value"}}. '
            'When you have the final answer, reply exactly: {"answer": "your answer"}. '
            "Never invent tool results and never write prose outside the JSON object."
        )

    async def execute(self, request: AgentRequest) -> AgentResponse:
        """Execute the agent loop and return the final answer and tool trace."""
        started = time.perf_counter()
        tools = self._available_tools(request)
        tool_by_name = {tool.name: tool for tool in tools}
        system_prompt = self._system_prompt(tools)
        conversation = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": request.input},
        ]
        tool_calls: list[ToolCall] = []
        usage = TokenUsage()
        iterations = 0
        output = ""
        status = "completed"

        for iteration in range(1, request.max_iterations + 1):
            iterations = iteration
            prompt = "\n".join(f"{turn['role'].upper()}: {turn['content']}" for turn in conversation)
            data = await self.ollama.generate(
                prompt=prompt,
                max_tokens=1024,
            )
            step_usage = _usage_from(data)
            usage.prompt_tokens += step_usage.prompt_tokens
            usage.completion_tokens += step_usage.completion_tokens
            usage.total_tokens += step_usage.total_tokens

            text = str(data.get("response", "")).strip()
            parsed = _parse_tool_call(text)
            if parsed is None or "answer" in parsed:
                output = parsed.get("answer", text) if parsed else text
                break

            tool_name = parsed["tool"]
            arguments = parsed["arguments"]
            tool_info = tool_by_name.get(tool_name)
            call_started = time.perf_counter()

            if tool_info is None:
                tool_calls.append(
                    ToolCall(
                        tool_name=tool_name,
                        arguments=arguments,
                        result="Unknown tool",
                        success=False,
                        execution_time_ms=(time.perf_counter() - call_started) * 1000,
                    )
                )
                conversation.append({"role": "user", "content": f"Error: unknown tool '{tool_name}'."})
                continue

            approved_explicitly = tool_name in (request.tools or [])
            if tool_info.requires_approval and not approved_explicitly:
                approval_id = f"apr-{uuid4().hex}"
                pending = ToolCall(
                    tool_name=tool_name,
                    arguments=arguments,
                    success=False,
                    requires_approval=True,
                    approval_id=approval_id,
                    execution_time_ms=(time.perf_counter() - call_started) * 1000,
                )
                tool_calls.append(pending)
                if request.require_approval:
                    logger.info("Approval required for tool", tool=tool_name, approval_id=approval_id)
                    raise ApprovalRequiredError(tool_name, approval_id)
                output = (
                    f"Tool '{tool_name}' requires approval before it can be executed "
                    f"(approval_id: {approval_id})."
                )
                status = "approval_required"
                break

            call_arguments = {**arguments}
            if tool_info.requires_approval:
                call_arguments["approved"] = True
            try:
                result = await self.tool_registry.call(tool_name, call_arguments)
                tool_calls.append(
                    ToolCall(
                        tool_name=tool_name,
                        arguments=arguments,
                        result=result,
                        success=True,
                        execution_time_ms=(time.perf_counter() - call_started) * 1000,
                    )
                )
                conversation.append(
                    {"role": "user", "content": f"Tool '{tool_name}' result: {json.dumps(result, default=str)}"}
                )
            except ApprovalRequiredError as exc:
                approval_id = str(exc.details.get("approval_id", f"apr-{uuid4().hex}"))
                tool_calls.append(
                    ToolCall(
                        tool_name=tool_name,
                        arguments=arguments,
                        result=exc.message,
                        success=False,
                        requires_approval=True,
                        approval_id=approval_id,
                        execution_time_ms=(time.perf_counter() - call_started) * 1000,
                    )
                )
                if request.require_approval:
                    raise
                output = f"Tool '{tool_name}' requires approval (approval_id: {approval_id})."
                status = "approval_required"
                break
            except ToolExecutionError as exc:
                tool_calls.append(
                    ToolCall(
                        tool_name=tool_name,
                        arguments=arguments,
                        result=exc.message,
                        success=False,
                        execution_time_ms=(time.perf_counter() - call_started) * 1000,
                    )
                )
                conversation.append({"role": "user", "content": f"Tool '{tool_name}' error: {exc.message}"})
        else:
            output = output or "Maximum tool iterations reached without a final answer."
            status = "max_iterations_reached"

        latency_ms = (time.perf_counter() - started) * 1000
        logger.info(
            "Agent execution complete",
            iterations=iterations,
            tool_calls=len(tool_calls),
            status=status,
            latency_ms=round(latency_ms, 2),
        )
        return AgentResponse(
            output=output,
            tool_calls=tool_calls,
            iterations=iterations,
            latency_ms=latency_ms,
            token_usage=usage,
            status=status,
        )


__all__ = ["AgentService", "SK_AVAILABLE", "_parse_tool_call"]
