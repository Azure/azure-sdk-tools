FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1

# Copy pre-installed Python packages downloaded on the ADO agent
# (where we have access to internal Azure feeds and proper credentials).
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-agent/site-packages/ /app/site-packages/
ENV PYTHONPATH=/app/site-packages:$PYTHONPATH

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
