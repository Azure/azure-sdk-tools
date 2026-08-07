FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV PIP_DISABLE_PIP_VERSION_CHECK=1
ENV PIP_DEFAULT_TIMEOUT=100
ENV PIP_RETRIES=5

# Install Node.js >= 22 (required for MCP stdio tools that use npx)
RUN tdnf install -y ca-certificates tar gzip && \
    curl -fsSL https://nodejs.org/dist/v22.15.0/node-v22.15.0-linux-x64.tar.xz \
      | tar -xJ --strip-components=1 -C /usr/local && \
    tdnf clean all

# Install dependencies first (layer caching)
# NOTE: paths are relative to the build context, which is the project directory
# (tools/sdk-ai-bots/azure-sdk-qa-bot-agent), not the repository root.
COPY requirements.txt .
RUN pip install --no-cache-dir --pre --timeout ${PIP_DEFAULT_TIMEOUT} --retries ${PIP_RETRIES} -r requirements.txt

# Copy application code
COPY config/ ./config/
COPY models/ ./models/
COPY prompts/ ./prompts/
COPY services/ ./services/
COPY tools/ ./tools/
COPY utils/ ./utils/
COPY skills/ ./skills/
COPY server.py .
COPY _version.py .

EXPOSE 8089

CMD ["python3", "-u", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8089"]
