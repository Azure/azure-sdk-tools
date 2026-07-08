FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV ENABLE_INSTRUMENTATION=true
ENV OTEL_EXPERIMENTAL_RESOURCE_DETECTORS=otel
ARG AZSDK_CLI_VERSION=0.6.3
ARG NODE_VERSION=22.12.0
ARG ADO_MCP_VERSION=2.7.0

# Install Azure SDK CLI (azsdk) used by `azsdk mcp`.
# Node.js >=20.19 is required by yargs@18 (transitive dep of ADO MCP).
RUN tdnf install -y ca-certificates curl tar gzip icu git \
	&& curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" \
		| tar -xJ --strip-components=1 -C /usr/local \
	&& npm install -g "@azure-devops/mcp@${ADO_MCP_VERSION}" \
	&& curl -fsSL -o /tmp/azsdk.tar.gz "https://github.com/Azure/azure-sdk-tools/releases/download/azsdk_${AZSDK_CLI_VERSION}/Azure.Sdk.Tools.Cli-standalone-linux-x64.tar.gz" \
	&& mkdir -p /opt/azsdk \
	&& tar -xzf /tmp/azsdk.tar.gz -C /opt/azsdk \
	&& AZSDK_BIN="$(find /opt/azsdk -maxdepth 3 -type f -name azsdk | head -n 1)" \
	&& test -n "$AZSDK_BIN" \
	&& ln -sf "$AZSDK_BIN" /usr/local/bin/azsdk \
	&& azsdk --version \
	&& rm -f /tmp/azsdk.tar.gz \
	&& tdnf clean all

# Install dependencies first (layer caching)
COPY requirements.txt .
RUN pip install --no-cache-dir --pre -r requirements.txt

# Copy shared modules
COPY config/ ./config/
COPY models/ ./models/
COPY prompts/ ./prompts/
COPY skills/ ./skills/
COPY tools/ ./tools/
COPY utils/ ./utils/

# Copy the specific agent
ARG AGENT_DIR=agents/chat_agent
COPY ${AGENT_DIR}/ ./

EXPOSE 8088

CMD ["python3", "-u", "init.py"]
