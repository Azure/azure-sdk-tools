FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app
ENV PYTHONUNBUFFERED=1
ENV ENABLE_INSTRUMENTATION=false
ENV ENABLE_SENSITIVE_DATA=false

COPY requirements.txt .
RUN pip install --no-cache-dir --pre -r requirements.txt azure-ai-agentserver-core==2.2.0b1 azure-ai-agentserver-responses==1.0.0b9

COPY config/ ./config/
COPY models/ ./models/
COPY services/ ./services/
COPY tools/ ./tools/
COPY utils/ ./utils/
COPY agents/teams_collection_agent/ ./agents/teams_collection_agent/

EXPOSE 8088
CMD ["python3", "-u", "-m", "agents.teams_collection_agent.init"]