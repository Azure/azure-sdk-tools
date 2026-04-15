# Stage 1: Download the azsdk CLI release
FROM debian:bookworm-slim AS cli-downloader

RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL -o /tmp/azsdk-cli.tar.gz \
       https://github.com/Azure/azure-sdk-tools/releases/download/azsdk_0.6.7/Azure.Sdk.Tools.Cli-standalone-linux-x64.tar.gz \
    && mkdir -p /azsdk-cli \
    && tar -xzf /tmp/azsdk-cli.tar.gz -C /azsdk-cli \
    && rm /tmp/azsdk-cli.tar.gz \
    && rm -rf /var/lib/apt/lists/*

# Stage 2: Build the Go backend
FROM mcr.microsoft.com/devcontainers/go:1.24 as go-builder

WORKDIR /app

# Set Go proxy to use public proxy
ENV GOPROXY=https://proxy.golang.org,direct

# Copy Go application files
COPY tools/sdk-ai-bots/azure-sdk-qa-bot-backend .

# Build Go application
RUN go mod download
RUN go vet -v
RUN go test -v
RUN CGO_ENABLED=0 go build -o go-backend

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0

WORKDIR /app

# Copy the Go backend binary and all runtime files
COPY --from=go-builder /app /app

# Copy the azsdk CLI tool
COPY --from=cli-downloader /azsdk-cli /usr/local/bin/azsdk-cli

# Configure environment for CLI tool
ENV PATH="/usr/local/bin/azsdk-cli:${PATH}"
ENV DOTNET_CLI_HOME="/tmp"

# Expose port for Go service
EXPOSE 8088

# Start Go backend
CMD ["/app/go-backend"]