# Build stage for Go backend
FROM golang:1.23-alpine AS go-build

WORKDIR /go/src/app
COPY . .

RUN go mod download
RUN go vet -v
RUN go test -v

RUN CGO_ENABLED=0 go build -o /go/bin/app

# Final stage with just Go backend
FROM alpine:latest

# Install ca-certificates for TLS connections
RUN apk --no-cache add ca-certificates

WORKDIR /app

# Copy Go binary from go-build stage
COPY --from=go-build /go/bin/app ./go-backend
# Copy all Go source code and directories
COPY --from=go-build /go/src/app ./

# Expose port for Go service
EXPOSE 8088

# Start Go backend
CMD ["./go-backend"]