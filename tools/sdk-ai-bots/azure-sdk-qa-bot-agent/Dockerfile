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
# NOTE: paths are relative to the build context, which is the project directory
# (tools/sdk-ai-bots/azure-sdk-qa-bot-agent), not the repository root.
COPY requirements.txt .
RUN pip install --no-cache-dir --pre --timeout ${PIP_DEFAULT_TIMEOUT} --retries ${PIP_RETRIES} -r requirements.txt

# Copy application code
COPY config/ ./config/
COPY models/ ./models/
COPY prompts/ ./prompts/
COPY services/ ./services/
COPY static/ ./static/
COPY tools/ ./tools/
COPY utils/ ./utils/
COPY skills/ ./skills/
COPY server.py .
COPY _version.py .

EXPOSE 8089

CMD ["python3", "-u", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8089"]
