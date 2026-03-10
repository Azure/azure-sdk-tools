import { stat } from "fs/promises";
import { resolve } from "path";
import semver from "semver";
import { describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";
import { SdkName } from "../shared/sdk-types.js";
import { getSdkDir } from "../src/fs.js";

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

const templateDir = {
  [SdkName.Js]: ["sdk", "template", "template"],
  [SdkName.Net]: ["sdk", "template", "Azure.Template"],
  [SdkName.Python]: ["TODO"],
};

describe.concurrent.each([SdkName.Js, SdkName.Net, SdkName.Python])(
  "%s",
  (sdkName) => {
    it("finds repo directory", async (ctx) => {
      const sdkDir = await getSdkDir(sdkName).catch(() => ctx.skip());
      const sdkDirStat = await stat(sdkDir);

      expect(sdkDirStat.isDirectory()).toBe(true);
      console.log(`SDK dir: ${sdkDir}`);
    });

    it("updates template", async (ctx) => {
      const sdkDir = await getSdkDir(sdkName).catch(() => ctx.skip());

      // TODO: use "git worktree" to copy to temp folder before destructive operation
      await execNpmExec(["tsp-client", "update"], {
        cwd: resolve(sdkDir, ...templateDir[sdkName]),
        logger: debugLogger,
        prefix: engCommonTspClient,
      });
    });
  },
);
