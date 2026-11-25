"""Application configuration managed through environment variables."""

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    # Application
    app_name: str = "Enterprise AI Assistant - AI Service"
    app_version: str = "0.1.0"
    debug: bool = False
    log_level: str = "INFO"

    # Ollama
    ollama_base_url: str = "http://localhost:11434"
    ollama_llm_model: str = "llama3.1:8b"
    ollama_embedding_model: str = "nomic-embed-text"
    ollama_num_ctx: int = 8192
    ollama_num_predict: int = 2048
    ollama_temperature: float = 0.1

    # API
    api_base_url: str = "http://localhost:5000"
    api_key: str = "changeme_ai_service_key"

    # Database
    postgres_host: str = "localhost"
    postgres_port: int = 5432
    postgres_db: str = "nexai"
    postgres_user: str = "nexai"
    postgres_password: str = "changeme"

    # Presidio
    presidio_enabled: bool = True
    presidio_analyzer_url: str = "http://localhost:3000"
    presidio_anonymizer_url: str = "http://localhost:3001"
    presidio_language: str = "en"
    presidio_min_score: float = 0.6

    # Feature Flags
    feature_enable_neo4j: bool = False
    feature_enable_mcp: bool = True
    feature_enable_evaluation: bool = True

    # Security
    prompt_injection_threshold: float = 0.8
    max_input_length: int = 10000
    max_context_length: int = 32000

    # RAG
    rag_top_k: int = 5
    rag_similarity_threshold: float = 0.65
    rag_max_context_chunks: int = 5
    rag_chunk_overlap: int = 100
    rag_chunk_size: int = 1000

    @property
    def database_url(self) -> str:
        """Build asynchronous PostgreSQL connection URL."""
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


settings = Settings()