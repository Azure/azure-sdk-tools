FROM mcr.microsoft.com/oss/otel/opentelemetry-collector-contrib:0.94.0 as otel

FROM busybox:latest as busybox
COPY --from=otel / /
ADD ./otel-collector-config.yml /otel-collector-config.yml
ADD ./startup.sh /startup.sh

EXPOSE 4317
EXPOSE 55678
EXPOSE 55679

ENTRYPOINT [ "/bin/sh", "-c", "/startup.sh" ]
