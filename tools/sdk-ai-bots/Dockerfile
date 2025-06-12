# Multi-stage build for backend services
FROM golang:1.23 as go-build

WORKDIR /go/src/app
COPY azure-sdk-qa-bot-backend/ .

RUN go mod download
RUN go vet -v
RUN go test -v

RUN CGO_ENABLED=0 go build -o /go/bin/app

FROM node:20 as node-build

WORKDIR /app
COPY azure-sdk-qa-bot-backend-shared/ .

RUN npm install

# Final stage with both Go and Node.js
FROM node:20-slim

# Install process manager for running multiple services and ca-certificates for TLS connections
RUN apt-get update && apt-get install -y ca-certificates && apt-get clean && rm -rf /var/lib/apt/lists/*
RUN npm install -g pm2

# Copy Go binary from go-build stage
COPY --from=go-build /go/bin/app /app/go-backend
# Copy all Go source code and directories
COPY --from=go-build /go/src/app /app

# Copy Node.js app from node-build stage
WORKDIR /app/node-backend
COPY --from=node-build /app/ ./

# Create PM2 config file
RUN echo '{\
  "apps": [\
    {\
      "name": "go-backend",\
      "script": "/app/go-backend",\
      "cwd": "/app",\
      "env": {\
        "PORT": "8088"\
      }\
    },\
    {\
      "name": "node-backend",\
      "script": "npm",\
      "args": "run dev:local",\
      "cwd": "/app/node-backend",\
      "env": {\
        "PORT": "3000"\
      }\
    }\
  ]\
}' > /app/ecosystem.config.json

# Expose ports for both services
EXPOSE 8088

# Start both services using PM2
CMD ["pm2-runtime", "start", "/app/ecosystem.config.json"]