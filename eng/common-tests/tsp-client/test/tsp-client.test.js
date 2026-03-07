import { dirname, resolve } from "path";
import semver from "semver";
import { fileURLToPath } from "url";
import { describe, expect, it } from "vitest";
import { execNpmExec } from "../src/exec.js";
import { debugLogger } from "../src/logger.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const engCommonTspClient = resolve(__dirname, "../../../common/tsp-client");

describe("tsp-client", () => {
  it("version parses as semver", async () => {
    const { stdout } = await execNpmExec(["tsp-client", "-v"], {
      logger: debugLogger,
      prefix: engCommonTspClient,
    });

    expect(semver.parse(stdout.trim())).toBeTruthy();
  });
});
