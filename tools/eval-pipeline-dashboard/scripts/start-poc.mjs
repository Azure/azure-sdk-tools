import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
process.env.HOST = "127.0.0.1";
process.env.PORT = "3201";
process.env.VALLY_LOCAL_BLOB_ROOT = resolve(root, "poc-blob");
process.env.VALLY_STAGING_ROOT = resolve(root, "poc-data", "staging");
process.env.VALLY_DB = resolve(root, "poc-data", "eval.db");
process.env.VALLY_BLOB_POLL_MS = "2000";

await import("../start.js");