import { stat } from "fs/promises";
import { resolve } from "path";
import semver from "semver";
import { describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";
import { getJsDir } from "../src/fs.js";

const engCommonTspClient = resolve("../../common/tsp-client");

describe("tsp-client", () => {
  it("version parses as semver", async () => {
    const { stdout } = await execNpmExec(["tsp-client", "-v"], {
      logger: debugLogger,
      prefix: engCommonTspClient,
    });

    expect(semver.parse(stdout.trim())).toBeTruthy();
  });
});

// TODO: Use describe.concurrent.each() and it.sequential() to run langs in parallel,
// but tests within lang in sequence
describe("js", () => {
  // TODO: skip JS tests if JS dir not exist

  it("finds repo directory", async () => {
    const jsDir = await getJsDir();
    const jsDirStat = await stat(jsDir);

    expect(jsDirStat.isDirectory()).toBe(true);
    console.log(`JS dir: ${jsDir}`);
  });

  it.skip("updates sdk/template/template", async () => {
    const jsDir = await getJsDir();

    await execNpmExec(["tsp-client", "update"], {
      cwd: resolve(jsDir, "sdk", "template", "template"),
      logger: debugLogger,
      prefix: engCommonTspClient,
    });
  });
});
