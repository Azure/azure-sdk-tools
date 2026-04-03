FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1

# Install dependencies from pre-downloaded wheels — no live PyPI access needed at build time.
# Wheels are downloaded by the pipeline onto the ADO agent before docker build runs.
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/requirements.txt .
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/wheels/ ./wheels/
RUN pip install --no-cache-dir --no-index --find-links=./wheels -r requirements.txt \
    && rm -rf ./wheels

# Copy application code
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/config/ ./config/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/models/ ./models/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/prompts/ ./prompts/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/services/ ./services/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/ ./tools/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/ ./utils/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/server.py .

EXPOSE 8080

CMD ["python3", "-u", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8080"]
