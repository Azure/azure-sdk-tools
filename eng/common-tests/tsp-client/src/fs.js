import { stat } from "fs/promises";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));

/**
 * @param {string} longName
 */
export async function getMatchingDir(longName) {
  // azure-sdk-for-js     -> js
  // azure-rest-api-specs -> specs
  const shortName = longName.substring(longName.lastIndexOf("-") + 1);

  const candidates = [
    resolve(__dirname, `../../../../../${longName}`),
    resolve(__dirname, `../../../../../${shortName}`),
  ];

  for (const candidate of candidates) {
    try {
      if ((await stat(candidate)).isDirectory()) {
        return candidate;
      }
    } catch {
      // continue to next candidate
    }
  }

  throw new Error(
    `Unable to find dir matching ${longName}. Checked: ${candidates.join(", ")}`,
  );
}
