"""Async client for local Ollama inference and embeddings."""

import time
from typing import Any

import httpx
from tenacity import retry, retry_if_exception_type, stop_after_attempt, wait_exponential

from app.config import settings
from app.core.exceptions import OllamaError
from app.core.logging import get_logger

logger = get_logger(__name__)


class OllamaClient:
    """Client for Ollama's REST API with retry logic."""

    def __init__(self, base_url: str | None = None) -> None:
        self.base_url = (base_url or settings.ollama_base_url).rstrip("/")
        self.client = httpx.AsyncClient(
            base_url=self.base_url,
            timeout=httpx.Timeout(300.0, connect=10.0),
            limits=httpx.Limits(max_connections=10, max_keepalive_connections=5),
        )

    async def close(self) -> None:
        """Close the HTTP client."""
        await self.client.aclose()

    async def __aenter__(self) -> "OllamaClient":
        return self

    async def __aexit__(self, *args: Any) -> None:
        await self.close()

    @retry(
        retry=retry_if_exception_type((httpx.RequestError, OllamaError)),
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=1, max=10),
        reraise=True,
    )
    async def is_healthy(self) -> bool:
        """Check if Ollama is reachable."""
        try:
            response = await self.client.get("/api/tags", timeout=5.0)
            response.raise_for_status()
            return True
        except (httpx.HTTPError, OllamaError) as exc:
            logger.warning("Ollama health check failed", error=str(exc))
            raise OllamaError(f"Ollama unreachable: {exc}") from exc

    @retry(
        retry=retry_if_exception_type(httpx.RequestError),
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=1, max=10),
        reraise=True,
    )
    async def list_models(self) -> list[str]:
        """List available models."""
        try:
            response = await self.client.get("/api/tags")
            response.raise_for_status()
            data = response.json()
            return [model["name"] for model in data.get("models", [])]
        except httpx.HTTPError as exc:
            raise OllamaError(f"Failed to list models: {exc}") from exc

    @retry(
        retry=retry_if_exception_type(httpx.RequestError),
        stop=stop_after_attempt(2),
        wait=wait_exponential(multiplier=1, min=1, max=5),
        reraise=True,
    )
    async def generate_embedding(self, text: str, model: str | None = None) -> list[float]:
        """Generate a single embedding vector for text."""
        model = model or settings.ollama_embedding_model
        try:
            response = await self.client.post(
                "/api/embeddings",
                json={"model": model, "prompt": text},
            )
            response.raise_for_status()
            data = response.json()
            return data["embedding"]
        except (httpx.HTTPError, KeyError) as exc:
            logger.error("Embedding generation failed", error=str(exc), model=model)
            raise OllamaError(f"Embedding generation failed: {exc}") from exc

    async def generate_embeddings(
        self, texts: list[str], model: str | None = None
    ) -> list[list[float]]:
        """Generate embeddings for multiple texts."""
        embeddings: list[list[float]] = []
        for text in texts:
            embedding = await self.generate_embedding(text, model)
            embeddings.append(embedding)
        return embeddings

    @retry(
        retry=retry_if_exception_type(httpx.RequestError),
        stop=stop_after_attempt(2),
        wait=wait_exponential(multiplier=1, min=1, max=5),
        reraise=True,
    )
    async def generate(
        self,
        prompt: str,
        model: str | None = None,
        system: str | None = None,
        temperature: float | None = None,
        max_tokens: int | None = None,
        context: list[dict[str, str]] | None = None,
    ) -> dict[str, Any]:
        """Generate a completion from the LLM."""
        model = model or settings.ollama_llm_model
        payload: dict[str, Any] = {
            "model": model,
            "prompt": prompt,
            "stream": False,
            "options": {
                "temperature": temperature if temperature is not None else settings.ollama_temperature,
                "num_predict": max_tokens or settings.ollama_num_predict,
                "num_ctx": settings.ollama_num_ctx,
            },
        }
        if system:
            payload["system"] = system
        if context:
            payload["context"] = context

        start = time.perf_counter()
        try:
            response = await self.client.post("/api/generate", json=payload)
            response.raise_for_status()
            data = response.json()
            logger.info(
                "LLM generation complete",
                model=model,
                latency_ms=(time.perf_counter() - start) * 1000,
                eval_count=data.get("eval_count", 0),
            )
            return data
        except httpx.HTTPError as exc:
            logger.error("LLM generation failed", error=str(exc), model=model)
            raise OllamaError(f"LLM generation failed: {exc}") from exc

    async def generate_stream(
        self,
        prompt: str,
        model: str | None = None,
        system: str | None = None,
        temperature: float | None = None,
    ):
        """Stream a completion token by token."""
        model = model or settings.ollama_llm_model
        payload: dict[str, Any] = {
            "model": model,
            "prompt": prompt,
            "stream": True,
            "options": {
                "temperature": temperature if temperature is not None else settings.ollama_temperature,
                "num_ctx": settings.ollama_num_ctx,
            },
        }
        if system:
            payload["system"] = system

        try:
            async with self.client.stream("POST", "/api/generate", json=payload) as response:
                response.raise_for_status()
                async for line in response.aiter_lines():
                    if line.strip():
                        import json

                        data = json.loads(line)
                        yield data.get("response", "")
                        if data.get("done", False):
                            break
        except httpx.HTTPError as exc:
            raise OllamaError(f"Streaming generation failed: {exc}") from exc

    async def pull_model(self, model: str) -> bool:
        """Pull a model if not available locally."""
        try:
            response = await self.client.post(
                "/api/pull", json={"name": model, "stream": False}
            )
            response.raise_for_status()
            return True
        except httpx.HTTPError as exc:
            logger.error("Model pull failed", error=str(exc), model=model)
            return False