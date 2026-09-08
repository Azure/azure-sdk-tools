// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Entry point for hosting the vally eval dashboard on Azure App Service (or
// locally). Serves the dashboard + API from a SQLite database, and — when a
// results directory is available — incrementally ingests new run folders so the
// dashboard updates automatically (after a browser refresh).
import { resolve, dirname } from "node:path";
import { existsSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Hono } from "hono";
import { serve } from "@hono/node-server";
import {
  initializeDatabase,
  DatabaseStore,
  createApp,
} from "@microsoft/vally-server";
import { startIngestWatcher } from "./ingest-watcher.js";
import { startArtifactSync } from "./lib/artifact-sync.js";
import { initializeRunMetadata } from "./lib/run-metadata.js";
import { mountPipelines } from "./pipelines.js";

const __dirname = dirname(fileURLToPath(import.meta.url));

// Azure App Service injects PORT; default to 3200 for local runs.
const port = Number(process.env.PORT) || 3200;
// Bind to all interfaces so the platform can route traffic to us.
const host = process.env.HOST || "0.0.0.0";

// Persistent SQLite store. Override with VALLY_DB (e.g. /home/data/eval.db on
// Azure, which stays writable even with run-from-package).
const dbPath = resolve(__dirname, process.env.VALLY_DB || "eval.db");
mkdirSync(dirname(dbPath), { recursive: true });

// Results directory for the legacy local-folder mode.
const resultsDir = process.env.VALLY_RESULTS
  ? resolve(process.env.VALLY_RESULTS)
  : resolve(__dirname, "results");
const localBlobRoot = process.env.VALLY_LOCAL_BLOB_ROOT
  ? resolve(process.env.VALLY_LOCAL_BLOB_ROOT)
  : null;
const stagingRoot = process.env.VALLY_STAGING_ROOT
  ? resolve(process.env.VALLY_STAGING_ROOT)
  : resolve(__dirname, ".blob-staging");
const blobPollMs = Number(process.env.VALLY_BLOB_POLL_MS) || 30000;

const db = initializeDatabase(dbPath);
initializeRunMetadata(db);

if (localBlobRoot && existsSync(localBlobRoot)) {
  mkdirSync(stagingRoot, { recursive: true });
  startArtifactSync({ db, blobRoot: localBlobRoot, stagingRoot, pollMs: blobPollMs });
} else if (existsSync(resultsDir)) {
  startIngestWatcher({ db, resultsDir });
} else {
  console.error(
    `[ingest] no results directory at ${resultsDir} — serving existing database only.\n` +
      `         Set VALLY_RESULTS to a directory of run folders to enable live updates.`,
  );
  const runCount = db.prepare("SELECT COUNT(*) AS c FROM runs").get().c;
  if (runCount === 0) {
    console.error(
      "[ingest] database is empty and no results directory was found; the dashboard will have no data.",
    );
  }
}

// Build the vally app (REST API + built-in dashboard at "/"), then wrap it in an
// outer app that adds a pipeline landing page and per-pipeline scoped reports.
const vallyApp = createApp(new DatabaseStore(db), { cors: false });

const app = new Hono();
// Pipeline landing page + scoped dashboards must be registered before the Vally app.
mountPipelines(app, vallyApp, db);
app.route("/", vallyApp);

console.error(`vally eval dashboard listening on http://${host}:${port}`);
console.error(`Dashboard: http://${host}:${port}/`);
serve({ fetch: app.fetch, port, hostname: host });
