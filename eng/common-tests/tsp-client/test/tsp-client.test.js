import { stat } from "fs/promises";
import { dirname, resolve } from "path";
import semver from "semver";
import { fileURLToPath } from "url";
import { describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";

// TODO: Add language enum

const __dirname = dirname(fileURLToPath(import.meta.url));
const engCommonTspClient = resolve(__dirname, "../../../common/tsp-client");

// TODO: take language enum param
async function getJsDir() {
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

// TODO: Use describe.concurrent.each() and it.sequential() to run langs in parallel,
// but tests within lang in sequence

describe("tsp-client", () => {
  it("version parses as semver", async () => {
    const { stdout } = await execNpmExec(["tsp-client", "-v"], {
      logger: debugLogger,
      prefix: engCommonTspClient,
    });

    expect(semver.parse(stdout.trim())).toBeTruthy();
  });

  // TODO: skip JS tests if JS dir not exist

  it("finds JS repo directory", async () => {
    const jsDir = await getJsDir();
    const jsDirStat = await stat(jsDir);

    expect(jsDirStat.isDirectory()).toBe(true);
    console.log(`JS dir: ${jsDir}`);
  });

  it("updates js/sdk/template/template", async () => {
    const jsDir = await getJsDir();

    await execNpmExec(["tsp-client", "update"], {
      cwd: resolve(jsDir, "sdk", "template", "template"),
      logger: debugLogger,
      prefix: engCommonTspClient,
    });
  });
});
