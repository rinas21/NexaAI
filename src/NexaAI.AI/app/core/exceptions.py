"""Custom exception hierarchy for the AI service."""


class AIServiceError(Exception):
    """Base exception for all AI service errors."""

    def __init__(
        self,
        message: str,
        error_code: str = "INTERNAL_ERROR",
        status_code: int = 500,
        details: dict | None = None,
    ) -> None:
        super().__init__(message)
        self.message = message
        self.error_code = error_code
        self.status_code = status_code
        self.details = details or {}


class ValidationError(AIServiceError):
    """Raised when input validation fails."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "VALIDATION_ERROR", 400, details)


class AuthenticationError(AIServiceError):
    """Raised when authentication fails."""

    def __init__(self, message: str = "Authentication failed") -> None:
        super().__init__(message, "AUTHENTICATION_ERROR", 401)


class AuthorizationError(AIServiceError):
    """Raised when authorization fails."""

    def __init__(self, message: str = "Access denied") -> None:
        super().__init__(message, "AUTHORIZATION_ERROR", 403)


class NotFoundError(AIServiceError):
    """Raised when a resource is not found."""

    def __init__(self, resource: str, identifier: str) -> None:
        super().__init__(
            f"{resource} '{identifier}' not found",
            "NOT_FOUND",
            404,
            {"resource": resource, "identifier": identifier},
        )


class OllamaError(AIServiceError):
    """Raised when Ollama inference fails."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "OLLAMA_ERROR", 503, details)


class EmbeddingError(AIServiceError):
    """Raised when embedding generation fails."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "EMBEDDING_ERROR", 500, details)


class RetrievalError(AIServiceError):
    """Raised when document retrieval fails."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "RETRIEVAL_ERROR", 500, details)


class PromptInjectionError(AIServiceError):
    """Raised when prompt injection is detected."""

    def __init__(self, message: str = "Prompt injection detected") -> None:
        super().__init__(message, "PROMPT_INJECTION", 400)


class PIIDetectionError(AIServiceError):
    """Raised when PII detection fails."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "PII_DETECTION_ERROR", 500, details)


class ToolExecutionError(AIServiceError):
    """Raised when a tool execution fails."""

    def __init__(self, tool_name: str, message: str, details: dict | None = None) -> None:
        super().__init__(
            f"Tool '{tool_name}' failed: {message}",
            "TOOL_EXECUTION_ERROR",
            500,
            {"tool_name": tool_name, **(details or {})},
        )


class ApprovalRequiredError(AIServiceError):
    """Raised when human approval is required before tool execution."""

    def __init__(self, tool_name: str, approval_id: str) -> None:
        super().__init__(
            f"Tool '{tool_name}' requires approval",
            "APPROVAL_REQUIRED",
            202,
            {"tool_name": tool_name, "approval_id": approval_id},
        )


class OutputValidationError(AIServiceError):
    """Raised when AI output fails validation."""

    def __init__(self, message: str, details: dict | None = None) -> None:
        super().__init__(message, "OUTPUT_VALIDATION_ERROR", 500, details)