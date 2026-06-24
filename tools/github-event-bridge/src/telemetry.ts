import { useAzureMonitor } from "@azure/monitor-opentelemetry";

// This module must be loaded via `--import` BEFORE server.ts (and therefore
// before logger.ts). useAzureMonitor() registers the global TracerProvider and
// LoggerProvider with Azure Monitor exporters. If it runs after logger.ts has
// already called logs.getLogger(), that logger stays bound to the no-op proxy
// provider and logger.emit(...) is silently dropped (empty `traces` table).
if (process.env.APPLICATIONINSIGHTS_CONNECTION_STRING) {
    useAzureMonitor();
    console.log("Azure Monitor OpenTelemetry initialized");
} else {
    console.log("APPLICATIONINSIGHTS_CONNECTION_STRING not set; skipping Azure Monitor");
}
