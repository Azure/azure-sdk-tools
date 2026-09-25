FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
ENV ENABLE_INSTRUMENTATION=true
ENV OTEL_EXPERIMENTAL_RESOURCE_DETECTORS=otel

RUN tdnf install -y ca-certificates git \
	&& tdnf clean all

COPY requirements.txt .
RUN pip install --no-cache-dir --pre -r requirements.txt

COPY config/ ./config/
COPY models/ ./models/
COPY prompts/ ./prompts/
COPY services/ ./services/
COPY skills/ ./skills/
COPY tools/ ./tools/
COPY utils/ ./utils/

ARG AGENT_DIR=agents/azure_mcp_server_agent
COPY ${AGENT_DIR}/ ./

EXPOSE 8088

CMD ["python3", "-u", "init.py"]
