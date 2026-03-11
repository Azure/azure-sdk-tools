import debug from "debug";
import { mkdtemp, rm, stat } from "fs/promises";
import { tmpdir } from "os";
import { dirname, join } from "path";
import semver from "semver";
import { simpleGit } from "simple-git";
import { fileURLToPath } from "url";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { execNpmExec } from "../shared/exec.js";
import { debugLogger } from "../shared/logger.js";
import { SdkName } from "../shared/sdk-types.js";
import { getRootSibling } from "../src/fs.js";

// Enable simple-git debug logging to improve console output
debug.enable("simple-git");

// absolute path of folder containing this file
const __dirname = dirname(fileURLToPath(import.meta.url));

// absolute path of repo containing eng/common/tsp-client
const engCommonTspClient = join(
  __dirname,
  "..",
  "..",
  "..",
  "common",
  "tsp-client",
);

/**
 * @param {string[]} args
 * @param {string} cwd
 */
async function execTspClient(args, cwd) {
  await execNpmExec(["tsp-client", "--debug", ...args], {
    cwd,
    logger: debugLogger,
    prefix: engCommonTspClient,
  });
}

describe("tsp-client", () => {
  it("version parses as semver", async () => {
    const { stdout } = await execNpmExec(["tsp-client", "-v"], {
      logger: debugLogger,
      prefix: engCommonTspClient,
    });

    expect(semver.parse(stdout.trim())).toBeTruthy();
  });

  it("finds spec dir", async (ctx) => {
    const specDir = await getRootSibling("azure-rest-api-specs").catch(() =>
      ctx.skip(),
    );

    expect((await stat(specDir)).isDirectory()).toBe(true);
  });
});

// if non-empty, test "tsp-client update" in this dir
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
    sdkDir = await getRootSibling(sdkName).catch(() => "");
    specDir = await getRootSibling("azure-rest-api-specs").catch(() => "");
  });

  beforeEach((ctx) => {
    // Skip any test if SDK dir is not cloned as tools sibling
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
            await simpleGit(sdkDir)
              .raw(["worktree", "remove", worktree, "--force"])
              .catch();
            await rm(worktree, { recursive: true, force: true });
          }
        }
      }
    });

    it("inits from url", async () => {
      const urlConfig =
        "https://github.com/Azure/azure-rest-api-specs/blob/c4213182795684aafcfe0ea51a0d91283ca979e1/specification/widget/data-plane/WidgetAnalytics/tspconfig.yaml";

      await execTspClient(["init", "-c", urlConfig], initUrlWorktree);
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

      await execTspClient(["init", "-c", localConfig], initLocalWorktree);
    });

    it("updates template", async (ctx) => {
      const templateDir = templateDirs[sdkName];

      if (templateDir.length === 0) {
        ctx.skip();
      }

      await execTspClient(["update"], join(updateWorktree, ...templateDir));
    });
  });
});
