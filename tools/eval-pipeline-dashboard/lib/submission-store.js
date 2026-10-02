import { createHash, randomUUID } from "node:crypto";
import { createReadStream } from "node:fs";
import { access, copyFile, link, mkdir, open, rm, stat } from "node:fs/promises";
import { constants } from "node:fs";
import { dirname, resolve, sep } from "node:path";
import { IngestionError } from "./ingestion-error.js";

export async function fileHash(path) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(path)) hash.update(chunk);
  return hash.digest("hex");
}

export function createSubmissionStore(root) {
  const base = resolve(root);
  const pathFor = (name) => {
    if (typeof name !== "string" || name.includes("\\") || name.split("/").some((part) => !part || part === "." || part === "..")) {
      throw new Error("Invalid archive object name.");
    }
    const path = resolve(base, name);
    if (!path.startsWith(`${base}${sep}`)) throw new Error("Archive object escaped its root.");
    return path;
  };
  return {
    async put(name, source, contentHash) {
      const destination = pathFor(name);
      await mkdir(dirname(destination), { recursive: true });
      const temporary = `${destination}.uploading-${randomUUID()}`;
      try {
        await copyFile(source, temporary);
        if (await fileHash(temporary) !== contentHash) throw new Error("Archive content hash changed during publication.");
        const handle = await open(temporary, "r+");
        try { await handle.sync(); } finally { await handle.close(); }
        try {
          // A hard link publishes atomically without replacing a concurrent upload.
          await link(temporary, destination);
        } catch (error) {
          if (error.code !== "EEXIST") throw error;
          if (await fileHash(destination) !== contentHash) {
            throw new IngestionError(409, "submission_conflict", "This submission identity already contains different content.");
          }
        }
        const info = await stat(destination);
        return { name, etag: contentHash, size: info.size, lastModified: info.mtime.toISOString() };
      } finally {
        await rm(temporary, { force: true });
      }
    },
    async download(name, destination, expectedHash) {
      await mkdir(dirname(destination), { recursive: true });
      await copyFile(pathFor(name), destination);
      if (await fileHash(destination) !== expectedHash) {
        throw new IngestionError(422, "archive_changed", "The stored submission content hash does not match its receipt.");
      }
    },
    async delete(name, expectedHash) {
      const path = pathFor(name);
      try {
        if (await fileHash(path) !== expectedHash) throw new IngestionError(409, "archive_changed", "Refusing to delete changed archive content.");
        await rm(path);
        return true;
      } catch (error) {
        if (error.code === "ENOENT") return false;
        throw error;
      }
    },
    async check() {
      await mkdir(base, { recursive: true });
      await access(base, constants.R_OK | constants.W_OK);
    },
  };
}