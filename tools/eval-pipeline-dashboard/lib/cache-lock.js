import { mkdir } from "node:fs/promises";
import { dirname } from "node:path";
import lockfile from "proper-lockfile";

export async function lockCache(dbPath) {
  await mkdir(dirname(dbPath), { recursive: true });
  try {
    return await lockfile.lock(dbPath, { realpath: false, retries: 0, stale: 120_000, update: 10_000 });
  } catch (error) {
    if (error.code === "ELOCKED") throw new Error("The Vally cache is in use. Stop the dashboard before maintenance or select a different cache path.", { cause: error });
    throw error;
  }
}