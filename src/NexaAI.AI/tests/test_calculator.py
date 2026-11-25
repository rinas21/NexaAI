"""Unit tests for the safe AST calculator tool and tool registry."""

import pytest

from app.agents.tools import ToolRegistry, calculator, datetime_tool
from app.core.exceptions import ToolExecutionError

pytestmark = pytest.mark.unit


def test_calculator_evaluates_arithmetic() -> None:
    assert calculator("2 + 3 * 4")["result"] == 14
    assert calculator("(2 + 3) * 4")["result"] == 20
    assert calculator("-5 + 2")["result"] == -3
    assert calculator("10 / 4")["result"] == 2.5
    assert calculator("2 ** 8")["result"] == 256


def test_calculator_supports_allowlisted_functions() -> None:
    assert calculator("sqrt(16)")["result"] == 4
    assert calculator("max(1, 7, 3)")["result"] == 7
    assert calculator("round(pi, 2)")["result"] == 3.14


def test_calculator_rejects_unsafe_expressions() -> None:
    with pytest.raises(ToolExecutionError):
        calculator("__import__('os').system('echo hacked')")
    with pytest.raises(ToolExecutionError):
        calculator("1; 2")
    with pytest.raises(ToolExecutionError):
        calculator("1 / 0")


def test_datetime_tool_returns_utc_timestamp() -> None:
    result = datetime_tool()
    assert result["timezone"] == "UTC"
    assert "T" in result["datetime"]
    assert result["unix_timestamp"] > 0


async def test_tool_registry_calls_sync_and_async_tools() -> None:
    registry = ToolRegistry()
    registry.register("calculator", calculator, description="Arithmetic")

    result = await registry.call("calculator", {"expression": "6 * 7"})
    assert result["result"] == 42
    assert [tool.name for tool in registry.list_tools()] == ["calculator"]

    with pytest.raises(ToolExecutionError):
        await registry.call("missing", {})
