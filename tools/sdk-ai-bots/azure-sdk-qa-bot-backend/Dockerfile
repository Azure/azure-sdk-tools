# Stage 1: Build the .NET CLI tool
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-builder

WORKDIR /build

# Copy the CLI project and its dependencies
COPY ../../azsdk-cli ./tools/azsdk-cli
COPY ../../codeowners-utils ./tools/codeowners-utils

# Restore and build the CLI tool
WORKDIR /build/tools/azsdk-cli
RUN dotnet restore Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj
RUN dotnet publish Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -c Release -o /azsdk-cli/publish --self-contained false

# Stage 2: Build the Go backend
FROM mcr.microsoft.com/devcontainers/go:1.24

WORKDIR /app

# Copy Go application files
COPY . .

# Build Go application
RUN go mod download
RUN go vet -v
RUN go test -v
RUN CGO_ENABLED=0 go build -o go-backend

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy the Go backend binary and all runtime files
COPY --from=1 /app /app

# Copy the .NET CLI tool
COPY --from=dotnet-builder /azsdk-cli/publish /usr/local/bin/azsdk-cli

# Configure environment for CLI tool
ENV PATH="/usr/local/bin/azsdk-cli:${PATH}"
ENV DOTNET_CLI_HOME="/tmp"

# Expose port for Go service
EXPOSE 8088

# Start Go backend
CMD ["/app/go-backend"]