FROM mcr.microsoft.com/azurelinux/base/nodejs:24 AS nodejs

FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1
ENV PIP_DEFAULT_TIMEOUT=100
ENV PIP_RETRIES=5

# Install Node.js from MCR mirror image (required for MCP stdio tools that use npx).
# This avoids pulling Node binaries from third-party hosts during image build.
COPY --from=nodejs /usr/local/ /usr/local/
RUN tdnf install -y ca-certificates && tdnf clean all

# Install dependencies first (layer caching)
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/requirements.txt .
RUN pip install --no-cache-dir --pre --timeout ${PIP_DEFAULT_TIMEOUT} --retries ${PIP_RETRIES} -r requirements.txt

# Copy application code
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/config/ ./config/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/models/ ./models/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/prompts/ ./prompts/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/services/ ./services/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/ ./tools/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/ ./utils/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/skills/ ./skills/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/server.py .
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/_version.py .

EXPOSE 8080

CMD ["python3", "-u", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8080"]
