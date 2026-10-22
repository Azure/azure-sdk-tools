FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1
ENV PIP_DEFAULT_TIMEOUT=100
ENV PIP_RETRIES=5

RUN tdnf install -y ca-certificates tar gzip git openssh-clients && \
  git --version && ssh -V && command -v ssh-keyscan && \
    tdnf clean all

# Install dependencies first (layer caching)
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/requirements.txt .
RUN pip install --no-cache-dir --pre --timeout ${PIP_DEFAULT_TIMEOUT} --retries ${PIP_RETRIES} -r requirements.txt

# Copy application code
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/config/ ./config/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/models/ ./models/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/prompts/ ./prompts/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/services/ ./services/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/static/ ./static/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/ ./tools/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/utils/ ./utils/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/skills/ ./skills/
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/server.py .
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/_version.py .

EXPOSE 8089

CMD ["python3", "-u", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8089"]
