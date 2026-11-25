#!/bin/bash
# Ollama Model Initialization Script
# Pulls required models on first startup

set -e

OLLAMA_HOST=${OLLAMA_HOST:-0.0.0.0:11434}
LLM_MODEL=${OLLAMA_LLM_MODEL:-llama3.1:8b}
EMBEDDING_MODEL=${OLLAMA_EMBEDDING_MODEL:-nomic-embed-text}

echo "Waiting for Ollama to be ready..."
until curl -sf http://localhost:11434/api/tags > /dev/null 2>&1; do
  sleep 2
done

echo "Ollama is ready. Checking for required models..."

# Function to pull model if not exists
pull_model() {
  local model=$1
  if ollama list | grep -q "^$model "; then
    echo "Model $model already exists, skipping pull."
  else
    echo "Pulling model: $model ..."
    ollama pull "$model"
    echo "Model $model pulled successfully."
  fi
}

# Pull LLM model
pull_model "$LLM_MODEL"

# Pull embedding model
pull_model "$EMBEDDING_MODEL"

echo "All required models are available."
echo "Available models:"
ollama list