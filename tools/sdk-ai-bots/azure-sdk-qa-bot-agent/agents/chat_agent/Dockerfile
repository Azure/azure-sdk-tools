FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1

# Install dependencies first (layer caching)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy shared modules
COPY config/ ./config/
COPY models/ ./models/
COPY tools/ ./tools/
COPY utils/ ./utils/

# Copy the specific agent (override via build arg for different agents)
ARG AGENT_DIR=agents/chat_agent
COPY ${AGENT_DIR}/ ./

EXPOSE 8088

CMD ["python3", "-u", "init.py"]
