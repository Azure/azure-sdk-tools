FROM mcr.microsoft.com/devcontainers/go:1.24

WORKDIR /app

COPY . .

RUN go mod download
RUN go vet -v
RUN go test -v

RUN CGO_ENABLED=0 go build -o go-backend

# Expose port for Go service
EXPOSE 8088

# Start Go backend
CMD ["./go-backend"]