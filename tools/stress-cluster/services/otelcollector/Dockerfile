FROM mcr.microsoft.com/oss/go/microsoft/golang:1.21 as build
RUN apt update -y && apt upgrade -y && apt install -y build-essential git

# this lets us build a custom otel-collector image, with a minimal set of 
# adapters, etc...
RUN go install go.opentelemetry.io/collector/cmd/builder@latest
COPY ./otel-builder.yml /otel-builder.yml
# builds to /tmp/dist
RUN builder --config=/otel-builder.yml

# this is the actual image we'll upload - it's got a shell so we can
# run startup.sh but is otherwise pristine.
FROM mcr.microsoft.com/cbl-mariner/busybox:2.0
COPY --from=build /tmp/dist/otelcol-custom /otelcol-custom
COPY ./otel-collector-config.yml /otel-collector-config.yml
ADD ./startup.sh /startup.sh

EXPOSE 4317
EXPOSE 55678
EXPOSE 55679

ENTRYPOINT [ "/bin/sh", "-c", "/startup.sh" ]
