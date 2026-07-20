FROM mcr.microsoft.com/azurelinux/base/python:3.12

WORKDIR /app

ENV PYTHONUNBUFFERED=1
# Tracing: emit OpenTelemetry spans/metrics to the project's Application Insights.
ENV ENABLE_INSTRUMENTATION=true
ENV OTEL_EXPERIMENTAL_RESOURCE_DETECTORS=otel

RUN tdnf install -y ca-certificates && tdnf clean all

# Install dependencies first (layer caching)
COPY requirements.txt .
RUN pip install --no-cache-dir --pre -r requirements.txt

# Copy the agent
COPY . .

EXPOSE 8088

CMD ["python3", "-u", "init.py"]
