import { createHash } from "node:crypto";
import { mkdir, mkdtemp, open, rm } from "node:fs/promises";
import { join } from "node:path";
import { Hono } from "hono";
import { MAX_BUNDLE_BYTES } from "./dashboard-bundle.js";
import { IngestionError } from "./ingestion-error.js";

async function receiveBundle(request, path, maxBytes) {
  const length = request.headers.get("content-length");
  if (length !== null && (!/^\d+$/.test(length) || Number(length) > maxBytes)) {
    throw new IngestionError(413, "upload_too_large", "Bundle exceeds the upload size limit.");
  }
  if (!request.body) throw new IngestionError(400, "empty_upload", "A ZIP body is required.");
  const reader = request.body.getReader();
  const file = await open(path, "wx");
  const hash = createHash("sha256");
  let size = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maxBytes) throw new IngestionError(413, "upload_too_large", "Bundle exceeds the upload size limit.");
      hash.update(value);
      await file.writeFile(value);
    }
    if (size === 0 || (length !== null && Number(length) !== size)) {
      throw new IngestionError(400, "incomplete_upload", "Upload length does not match the request.");
    }
    await file.sync();
    return hash.digest("hex");
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    await file.close();
  }
}

export function mountIngestionRoutes(app, { service, authorize, stagingRoot, maxBytes = MAX_BUNDLE_BYTES, maxConcurrent = 2 }) {
  const routes = new Hono();
  let activeUploads = 0;
  routes.onError((error, context) => {
    const expected = error instanceof IngestionError;
    const status = expected ? error.status : 503;
    if (status === 401) context.header("WWW-Authenticate", "Bearer");
    if (status === 503 || status === 429) context.header("Retry-After", "5");
    return context.json({ error: {
      code: expected ? error.code : "ingestion_unavailable",
      message: expected ? error.message : "Ingestion is temporarily unavailable. Retry the same bundle.",
    } }, status);
  });
  routes.use("*", async (context, next) => {
    context.header("Cache-Control", "no-store");
    context.set("publisher", await authorize(context));
    await next();
  });
  routes.post("/", async (context) => {
    if (context.req.header("content-type")?.split(";")[0].trim().toLowerCase() !== "application/zip") {
      throw new IngestionError(415, "unsupported_media_type", "Use Content-Type: application/zip.");
    }
    if (activeUploads >= maxConcurrent) throw new IngestionError(429, "upload_busy", "Too many uploads. Retry shortly.");
    activeUploads++;
    let temporary;
    try {
      await mkdir(stagingRoot, { recursive: true });
      temporary = await mkdtemp(join(stagingRoot, "upload-"));
      const bundlePath = join(temporary, "dashboard-bundle.zip");
      const contentHash = await receiveBundle(context.req.raw, bundlePath, maxBytes);
      const expected = context.req.header("x-content-sha256");
      if (expected !== undefined && (!/^[a-fA-F0-9]{64}$/.test(expected) || expected.toLowerCase() !== contentHash)) {
        throw new IngestionError(400, "checksum_mismatch", "Uploaded content does not match X-Content-SHA256.");
      }
      const result = await service.accept({ bundlePath, contentHash, publisher: context.get("publisher") });
      context.header("Location", result.receipt.statusUrl);
      return context.json({ ...result.receipt, duplicate: result.duplicate }, result.duplicate && ["succeeded", "failed"].includes(result.receipt.status) ? 200 : 202);
    } finally {
      try {
        if (temporary) await rm(temporary, { recursive: true, force: true });
      } finally {
        activeUploads--;
      }
    }
  });
  routes.get("/:id", async (context) => context.json(await service.get(context.req.param("id"), context.get("publisher"))));
  app.route("/api/ingestions", routes);
}