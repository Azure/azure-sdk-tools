// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { Hono } from "hono";
import { serve } from "@hono/node-server";
import {
  initializeDatabase,
  DatabaseStore,
  createApp,
} from "@microsoft/vally-server";
import { initializeRunMetadata } from "./lib/run-metadata.js";
import { mountPipelines } from "./pipelines.js";
import { createPublisherAuthorizer, hostGuard } from "./lib/publisher-auth.js";
import { createIngestionService } from "./lib/ingestion-service.js";
import { mountIngestionRoutes } from "./lib/ingestion-routes.js";
import { openStorage, readStorageConfig } from "./lib/storage-config.js";
import { mountOperationalRoutes } from "./lib/operational-routes.js";
import { lockCache } from "./lib/cache-lock.js";

const __dirname = dirname(fileURLToPath(import.meta.url));

// Azure App Service injects PORT; default to 3200 for local runs.
const port = Number(process.env.PORT) || 3200;
const host = process.env.HOST || "127.0.0.1";
const allowedHosts = [process.env.WEBSITE_HOSTNAME, ...(process.env.VALLY_ALLOWED_HOSTS ?? "").split(",")]
  .filter(Boolean).map((value) => value.trim());
const authorize = createPublisherAuthorizer({
  anonymousLocal: process.env.VALLY_LOCAL_POC === "true",
  bindHost: host,
  tenantId: process.env.VALLY_INGEST_TENANT_ID,
  audience: process.env.VALLY_INGEST_AUDIENCE,
  clientIds: (process.env.VALLY_INGEST_CLIENT_IDS ?? "").split(",").map((id) => id.trim()).filter(Boolean),
});

const config = readStorageConfig(process.env, __dirname);
const unlock = await lockCache(config.dbPath);
let db, journal, service, httpServer, closing;
function shutdown() {
  closing ??= (async () => {
    if (httpServer) await new Promise((done) => httpServer.close(done));
    await service?.stop();
    await journal?.close();
    db?.close();
    await unlock();
  })();
  return closing;
}
function fatal(error) {
  console.error(error.message);
  process.exitCode = 1;
  void shutdown().catch((failure) => console.error(failure.message));
}
try {
  db = initializeDatabase(config.dbPath);
  initializeRunMetadata(db);
  const storage = await openStorage(config);
  journal = storage.journal;
  service = createIngestionService({ db, journal, store: storage.store, stagingRoot: config.stagingRoot });
  const vallyApp = createApp(new DatabaseStore(db), { cors: false, allowedHosts });
  const app = new Hono();
  app.use("*", hostGuard(allowedHosts));
  mountIngestionRoutes(app, { service, authorize, stagingRoot: config.stagingRoot });
  mountOperationalRoutes(app, { service, provider: config.provider });
  // No viewer login: hosted access is restricted by Azure's private network boundary.
  mountPipelines(app, vallyApp, db);
  app.route("/", vallyApp);
  httpServer = serve({ fetch: app.fetch, port, hostname: host }, () => {
    console.error(`Dashboard: http://${host}:${port}/ (storage: ${config.provider}; no artifact scanning)`);
    void service.start().catch(fatal);
  });
  httpServer.requestTimeout = 60_000;
  httpServer.on("error", fatal);
  process.once("SIGINT", () => void shutdown().catch(fatal));
  process.once("SIGTERM", () => void shutdown().catch(fatal));
} catch (error) {
  await shutdown();
  throw error;
}
