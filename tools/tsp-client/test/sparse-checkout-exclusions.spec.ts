import { execFileSync } from "node:child_process";
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";
import { compile, NodeHost } from "@typespec/compiler";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { stringify } from "yaml";
import { syncCommand } from "../src/commands.js";
import { readTspLocation } from "../src/fs.js";
import * as gitHelpers from "../src/git.js";
import { updateExistingTspLocation, writeTspLocationYaml } from "../src/utils.js";

const cloneRepo = gitHelpers.cloneRepo;
const project = "specification/Example";
let root: string;
let sdk: string;
let repository: string;
let commit: string;
let ignoreCase: boolean;
let branchConditionalCase: boolean;

function git(cwd: string, ...args: string[]) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

async function put(root: string, path: string, data: string) {
  const target = join(root, path);
  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, data);
}

async function sync(
  exclusions?: unknown,
  additionalDirectories = ["common/Shared"],
  local = false,
  entrypointFile?: string,
) {
  await put(
    sdk,
    "tsp-location.yaml",
    stringify({
      directory: project,
      commit,
      repo: "example/specs",
      additionalDirectories,
      entrypointFile,
      sparseCheckoutExclusions: exclusions,
    }),
  );
  await syncCommand({
    "output-dir": sdk,
    "local-spec-repo": local ? join(repository, project) : undefined,
  });
  return join(sdk, "TempTypeSpecFiles");
}

describe("remote sparse checkout exclusions", () => {
  beforeEach(async () => {
    ignoreCase = true;
    branchConditionalCase = false;
    root = await mkdtemp(join(tmpdir(), "tsp-sparse-exclusions-"));
    sdk = join(root, "sdk");
    repository = join(root, "spec");
    await mkdir(sdk);
    await mkdir(repository);
    for (const [path, content] of Object.entries({
      "specification/.gitattributes": "*.TSP text eol=crlf\n",
      [`${project}/notes`]: "A regular file, not a directory.\n",
      [`${project}/main.tsp`]: 'import "../Shared/models.tsp";\nmodel Pet extends Shared {}\n',
      [`${project}/stable/2022-01-01/examples/legacy.json`]: '{"legacy":true}',
      [`${project}/examples/stable/current.json`]: '{"current":true}',
      "common/Shared/models.tsp": "model Shared { name: string; }\n",
      "common/Shared/stable/required.json": '{"required":true}',
    }))
      await put(repository, path, content);
    git(repository, "init", "-q");
    git(repository, "add", "--force", ".");
    git(
      repository,
      "-c",
      "user.name=Test",
      "-c",
      "user.email=test@example.test",
      "-c",
      "commit.gpgsign=false",
      "-c",
      "core.hooksPath=/dev/null",
      "commit",
      "-qm",
      "Initial spec",
    );
    commit = git(repository, "rev-parse", "HEAD");
    git(repository, "config", "uploadpack.allowFilter", "true");
    git(sdk, "init", "-q");
    await put(sdk, "eng/emitter-package.json", "{}");
    vi.spyOn(gitHelpers, "cloneRepo").mockImplementation(async (cwd, target) => {
      await cloneRepo(cwd, target, pathToFileURL(repository).href);
      git(target, "config", "core.ignoreCase", String(ignoreCase));
      git(target, "config", "core.autocrlf", "false");
      if (branchConditionalCase) {
        git(target, "branch", "-M", "sparse-fixture");
        await put(target, ".git/branch-case.cfg", "[core]\n\tignoreCase = true\n");
        git(target, "config", "includeIf.onbranch:sparse-fixture.path", "./branch-case.cfg");
      }
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    await rm(root, { recursive: true, force: true });
  });

  it("omits only the configured project child and preserves existing staged imports", async () => {
    const staged = await sync();
    const baseline = await readFile(join(staged, "Example/main.tsp"), "utf8");
    expect(baseline).toContain("\r\n");
    await sync(["stable", "notes", "missing"]);
    expect((await readdir(join(staged, "Example"))).sort()).toEqual([
      "examples",
      "main.tsp",
      "notes",
    ]);
    expect(await readFile(join(staged, "Example/main.tsp"), "utf8")).toBe(baseline);
    expect(await readFile(join(staged, "Example/examples/stable/current.json"), "utf8")).toBe(
      '{"current":true}',
    );
    expect(await readFile(join(staged, "Shared/stable/required.json"), "utf8")).toBe(
      '{"required":true}',
    );
    const program = await compile(NodeHost, join(staged, "Example/main.tsp"), { noEmit: true });
    expect(program.diagnostics).toEqual([]);
  });

  it.each([undefined, []])("keeps full project selection with opt-in %j", async (exclusions) => {
    const staged = await sync(exclusions);
    expect(
      await readFile(join(staged, "Example/stable/2022-01-01/examples/legacy.json"), "utf8"),
    ).toBe('{"legacy":true}');
  });

  it("honors Git's case-sensitive selection when core.ignoreCase is false", async () => {
    ignoreCase = false;
    const staged = await sync(["Stable"]);
    expect(await readdir(join(staged, "Example"))).toContain("stable");
  });

  it("clears excluded directories left by a previous full sync, not unrelated files", async () => {
    vi.stubEnv("EDITOR", "editor-for-unrelated-processes");
    const staged = await sync();
    await put(staged, "Example/keep.txt", "keep");
    await sync(["Stable"]);
    expect(await readdir(join(staged, "Example"))).not.toContain("stable");
    expect(await readFile(join(staged, "Example/keep.txt"), "utf8")).toBe("keep");
  });

  it("uses the checkout's branch-dependent case matching for stale directories", async () => {
    ignoreCase = false;
    branchConditionalCase = true;
    const staged = await sync();
    expect(await readdir(join(staged, "Example"))).toContain("stable");
    await sync(["Stable"]);
    expect(await readdir(join(staged, "Example"))).not.toContain("stable");
  });

  it.each([
    "specification",
    `${project}/stable`,
    `${project}/Stable/2022-01-01`,
    "elsewhere/example",
  ])("rejects conflicting additional directory %s before cloning", async (path) => {
    await expect(sync(["stable"], [path])).rejects.toThrow(
      /sparseCheckoutExclusions.*additionalDirectories/,
    );
    expect(gitHelpers.cloneRepo).not.toHaveBeenCalled();
  });

  it.each(["stable", "stable/main.tsp"])(
    "rejects excluded entrypoint %s before cloning",
    async (entrypoint) => {
      await expect(sync(["stable"], [], false, entrypoint)).rejects.toThrow(
        /sparseCheckoutExclusions.*entrypointFile/,
      );
      expect(gitHelpers.cloneRepo).not.toHaveBeenCalled();
    },
  );

  it.each(["stable", ["stable/**"], [".."], ["NUL"], ["stable\n"]])(
    "rejects invalid exclusions %j",
    async (value) => {
      await expect(sync(value)).rejects.toThrow(/list of literal direct child/);
      expect(gitHelpers.cloneRepo).not.toHaveBeenCalled();
    },
  );

  it("retains the setting when rewriting a location after an update", async () => {
    await sync(["stable"]);
    const updated = await updateExistingTspLocation({ commit: "new-commit" }, sdk);
    await writeTspLocationYaml(updated, sdk);
    expect(await readTspLocation(sdk)).toMatchObject({
      commit: "new-commit",
      sparseCheckoutExclusions: ["stable"],
    });
  });

  it("does not filter local spec inputs", async () => {
    const staged = await sync(["stable"], undefined, true);
    expect(
      await readFile(join(staged, "Example/stable/2022-01-01/examples/legacy.json"), "utf8"),
    ).toBe('{"legacy":true}');
    expect(gitHelpers.cloneRepo).not.toHaveBeenCalled();
  });
});
