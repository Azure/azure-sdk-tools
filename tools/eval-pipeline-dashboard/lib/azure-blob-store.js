import { mkdir, stat } from "node:fs/promises";
import { dirname } from "node:path";
import { fileHash } from "./submission-store.js";
import { MAX_BUNDLE_BYTES } from "./dashboard-bundle.js";
import { IngestionError } from "./ingestion-error.js";

const isConflict = (error) => [409, 412].includes(error.statusCode);

export function validateObjectName(name) {
  if (typeof name !== "string" || !name.startsWith("v1/") || name.includes("\\") ||
      name.split("/").some((part) => !part || part === "." || part === "..")) {
    throw new Error("Invalid archive object name.");
  }
  return name;
}

export function createAzureBlobStore(containerClient) {
  return {
    async put(name, source, contentHash) {
      const blob = containerClient.getBlockBlobClient(validateObjectName(name));
      const size = (await stat(source)).size;
      if (size > MAX_BUNDLE_BYTES || await fileHash(source) !== contentHash) {
        throw new IngestionError(422, "archive_changed", "Bundle changed before archive publication.");
      }
      try {
        await blob.uploadFile(source, {
          conditions: { ifNoneMatch: "*" },
          metadata: { sha256: contentHash },
          blobHTTPHeaders: { blobContentType: "application/zip" },
        });
      } catch (error) {
        if (!isConflict(error)) throw error;
        const existing = await blob.getProperties();
        if (existing.metadata?.sha256 !== contentHash || existing.contentLength !== size) {
          throw new IngestionError(409, "submission_conflict", "This immutable archive path already contains different content.");
        }
      }
      const properties = await blob.getProperties();
      return {
        name, etag: properties.etag, sha256: contentHash,
        size: properties.contentLength, lastModified: properties.lastModified.toISOString(),
      };
    },
    async download(name, destination, expectedHash) {
      const blob = containerClient.getBlobClient(validateObjectName(name));
      const properties = await blob.getProperties();
      if (properties.contentLength > MAX_BUNDLE_BYTES || properties.metadata?.sha256 !== expectedHash) {
        throw new IngestionError(422, "archive_changed", "Stored bundle does not match its receipt.");
      }
      await mkdir(dirname(destination), { recursive: true });
      await blob.downloadToFile(destination, 0, undefined, { conditions: { ifMatch: properties.etag } });
      if (await fileHash(destination) !== expectedHash) {
        throw new IngestionError(422, "archive_changed", "Stored bundle failed content verification.");
      }
    },
    async delete(name, expectedHash) {
      const blob = containerClient.getBlobClient(validateObjectName(name));
      let properties;
      try { properties = await blob.getProperties(); }
      catch (error) { if (error.statusCode === 404) return false; throw error; }
      if (properties.metadata?.sha256 !== expectedHash) {
        throw new IngestionError(409, "archive_changed", "Refusing to delete an archive with a different content hash.");
      }
      await blob.delete({ conditions: { ifMatch: properties.etag } });
      return true;
    },
    async check() { await containerClient.getProperties(); },
  };
}