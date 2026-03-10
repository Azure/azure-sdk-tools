import { stat } from "fs/promises";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));

/**
 * @param {import("../shared/sdk-types.js").SdkName} sdkName
 */
export async function getSdkDir(sdkName) {
  const lang = sdkName.replace("azure-sdk-for-", "");

  // look for full name (eg "azure-sdk-for-js") or short name (eg "js")
  const candidates = [
    resolve(__dirname, `../../../../../${sdkName}`),
    resolve(__dirname, `../../../../../${lang}`),
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
    `Unable to find ${sdkName} repo clone. Checked: ${candidates.join(", ")}`,
  );
}
