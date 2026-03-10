import debug from "debug";
import { mkdtemp, rm, stat } from "fs/promises";
import { tmpdir } from "os";
import { join, resolve } from "path";
import semver from "semver";
import { simpleGit } from "simple-git";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";
import { SdkName } from "../shared/sdk-types.js";
import { getSdkDir } from "../src/fs.js";

// Enable simple-git debug logging to improve console output
debug.enable("simple-git");

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
    /** @type {string} */
    let sdkDir;

    beforeEach(async (ctx) => {
      sdkDir = await getSdkDir(sdkName).catch(() => ctx.skip());
    });

    it("finds sdk dir", async () => {
      const sdkDirStat = await stat(sdkDir);

      expect(sdkDirStat.isDirectory()).toBe(true);
      console.log(`SDK dir: ${sdkDir}`);
    });

    describe("worktree tests", () => {
      /** @type {string} */
      let worktree;

      beforeEach(async () => {
        const lang = sdkName.replace("azure-sdk-for-", "");
        worktree = await mkdtemp(join(tmpdir(), `tsp-client-test-${lang}-`));

        await simpleGit(sdkDir).raw(["worktree", "add", worktree, "--detach"]);
      });

      afterEach(async () => {
        try {
          await simpleGit(sdkDir).raw([
            "worktree",
            "remove",
            worktree,
            "--force",
          ]);
        } catch {
          // Worktree may not have been created
        }
        await rm(worktree, { recursive: true, force: true });
      });

      it("inits from url", async () => {
        const url =
          "https://github.com/Azure/azure-rest-api-specs/blob/c4213182795684aafcfe0ea51a0d91283ca979e1/specification/widget/data-plane/WidgetAnalytics/tspconfig.yaml";

        await execNpmExec(["tsp-client", "init", "-c", url], {
          cwd: worktree,
          logger: debugLogger,
          prefix: engCommonTspClient,
        });
      });

      it("updates template", async () => {
        await execNpmExec(["tsp-client", "update"], {
          cwd: join(worktree, ...templateDir[sdkName]),
          logger: debugLogger,
          prefix: engCommonTspClient,
        });
      });
    });
  },
);
