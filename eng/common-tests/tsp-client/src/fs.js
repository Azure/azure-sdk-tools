import { stat } from "fs/promises";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));

// TODO: take language enum param
export async function getJsDir() {
  // TODO: Could fallback to env var, put I prefer convention over config
  const candidates = [
    resolve(__dirname, "../../../../../azure-sdk-for-js"),
    resolve(__dirname, "../../../../../js"),
  ];

  for (const candidate of candidates) {
    try {
      if ((await stat(candidate)).isDirectory()) {
        return candidate;
      }
    } catch {
      // Continue to the next candidate if this path does not exist.
    }
  }

  throw new Error(
    `Unable to find JS repo clone. Checked: ${candidates.join(", ")}`,
  );
}
