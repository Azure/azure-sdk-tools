import debug from "debug";
import { mkdtemp, rm, stat } from "fs/promises";
import { tmpdir } from "os";
import { join, resolve } from "path";
import semver from "semver";
import { simpleGit } from "simple-git";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";
import { SdkName } from "../shared/sdk-types.js";
import { getMatchingDir } from "../src/fs.js";

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

  it("finds spec dir", async (ctx) => {
    const specDir = await getMatchingDir("azure-rest-api-specs").catch(() =>
      ctx.skip(),
    );

    expect((await stat(specDir)).isDirectory()).toBe(true);
  });
});

const templateDirs = {
  [SdkName.Go]: [],
  [SdkName.Java]: [],
  [SdkName.Js]: ["sdk", "template", "template"],
  [SdkName.Net]: ["sdk", "template", "Azure.Template"],
  [SdkName.Python]: [],
};

describe.concurrent.each([
  SdkName.Go,
  SdkName.Java,
  SdkName.Js,
  SdkName.Net,
  SdkName.Python,
])("%s", (sdkName) => {
  /** @type {string} */
  let sdkDir;

  /** @type {string} */
  let specDir;

  beforeAll(async () => {
    sdkDir = await getMatchingDir(sdkName).catch(() => "");
    specDir = await getMatchingDir("azure-rest-api-specs").catch(() => "");
  });

  beforeEach((ctx) => {
    if (!sdkDir) {
      ctx.skip();
    }
  });

  it("finds sdk dir", async () => {
    const sdkDirStat = await stat(sdkDir);
    expect(sdkDirStat.isDirectory()).toBe(true);
  });

  describe("worktree tests", () => {
    /** @type {string} */
    let initUrlWorktree;

    /** @type {string} */
    let initLocalWorktree;

    /** @type {string} */
    let updateWorktree;

    // worktrees from the same source repo must be created/removed sequentially (not in parallel)
    beforeAll(async () => {
      if (sdkDir) {
        const lang = sdkName.replace("azure-sdk-for-", "");

        initUrlWorktree = await mkdtemp(
          join(tmpdir(), `tsp-client-test-initurl-${lang}-`),
        );

        if (specDir) {
          initLocalWorktree = await mkdtemp(
            join(tmpdir(), `tsp-client-test-initlocal-${lang}-`),
          );
        }

        const templateDir = templateDirs[sdkName];
        if (templateDir.length > 0) {
          updateWorktree = await mkdtemp(
            join(tmpdir(), `tsp-client-test-update-${lang}-`),
          );
        }

        for (const worktree of [
          initUrlWorktree,
          initLocalWorktree,
          updateWorktree,
        ]) {
          if (worktree) {
            await simpleGit(sdkDir).raw([
              "worktree",
              "add",
              worktree,
              "--detach",
            ]);
          }
        }
      }
    });

    afterAll(async () => {
      if (sdkDir) {
        for (const worktree of [
          initUrlWorktree,
          initLocalWorktree,
          updateWorktree,
        ]) {
          if (worktree) {
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
          }
        }
      }
    });

    it("inits from url", async () => {
      // const url =
      //   "https://github.com/Azure/azure-rest-api-specs/blob/c4213182795684aafcfe0ea51a0d91283ca979e1/specification/widget/data-plane/WidgetAnalytics/tspconfig.yaml";

      // test widget rust config
      const urlConfig =
        "https://github.com/Azure/azure-rest-api-specs/blob/1c6ba5522dfdf969d4e541737e8969f542a80fd5/specification/widget/data-plane/WidgetAnalytics/tspconfig.yaml";

      await execNpmExec(["tsp-client", "--debug", "init", "-c", urlConfig], {
        cwd: initUrlWorktree,
        logger: debugLogger,
        prefix: engCommonTspClient,
      });
    });

    it("inits from local", async (ctx) => {
      if (!specDir) {
        ctx.skip();
      }

      const localConfig = join(
        specDir,
        "specification",
        "widget",
        "data-plane",
        "WidgetAnalytics",
        "tspconfig.yaml",
      );

      await execNpmExec(["tsp-client", "--debug", "init", "-c", localConfig], {
        cwd: initLocalWorktree,
        logger: debugLogger,
        prefix: engCommonTspClient,
      });
    });

    it("updates template", async (ctx) => {
      const templateDir = templateDirs[sdkName];

      if (templateDir.length === 0) {
        ctx.skip();
      }

      await execNpmExec(["tsp-client", "--debug", "update"], {
        cwd: join(updateWorktree, ...templateDir),
        logger: debugLogger,
        prefix: engCommonTspClient,
      });
    });
  });
});
