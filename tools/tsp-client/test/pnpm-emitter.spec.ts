import {
  cp,
  lstat,
  mkdtemp,
  mkdir,
  readFile,
  readdir,
  rm,
  stat,
  symlink,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  generateCommand,
  generateConfigFilesCommand,
  generateLockFileCommand,
  generateLockFileCommandCore,
  installDependencies,
  syncCommand,
} from "../src/commands.js";
import { getRepoRoot } from "../src/git.js";
import * as workspaceFs from "../src/fs.js";
import { pnpmCommand, pnpmWorkspaceContent, publishEmitterFile } from "../src/pnpm.js";
import * as pnpm from "../src/pnpm.js";

vi.mock("../src/git.js", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/git.js")>()),
  getRepoRoot: vi.fn(),
}));

async function readFiles(dir: string): Promise<Record<string, string>> {
  const files: Record<string, string> = {};
  async function visit(path: string, prefix: string): Promise<void> {
    for (const entry of await readdir(path, { withFileTypes: true })) {
      const name = prefix ? `${prefix}/${entry.name}` : entry.name;
      if (entry.isDirectory()) await visit(join(path, entry.name), name);
      else if (entry.isFile())
        files[name] = (await readFile(join(path, entry.name))).toString("base64");
    }
  }
  await visit(dir, "");
  return files;
}

let root: string | undefined;
afterEach(async () => {
  if (root) await rm(root, { recursive: true, force: true });
  root = undefined;
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

describe("pnpm emitter dependencies", () => {
  it("stages the committed pnpm lockfile for generation", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-lock-"));
    const eng = join(root, "eng");
    const output = join(root, "sdk");
    const spec = join(root, "spec");
    await Promise.all([eng, output, spec].map((path) => mkdir(path, { recursive: true })));
    await writeFile(join(eng, "emitter-package.json"), '{"dependencies":{}}');
    await writeFile(join(eng, "emitter-package-pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
    await writeFile(join(spec, "main.tsp"), "namespace Example;\n");
    await writeFile(
      join(output, "tsp-location.yaml"),
      "directory: spec\ncommit: test\nrepo: Azure/test\nadditionalDirectories: []\n",
    );
    vi.mocked(getRepoRoot).mockResolvedValue(root);

    await syncCommand({ "output-dir": output, "local-spec-repo": spec });

    expect(await readFile(join(output, "TempTypeSpecFiles", "pnpm-lock.yaml"), "utf8")).toBe(
      "lockfileVersion: '9.0'\n",
    );
    // Switching the same saved workspace back to npm must not retain a stale pnpm lock.
    const selected = join(eng, "emitter-package-pnpm-lock.yaml");
    const staged = join(output, "TempTypeSpecFiles", "pnpm-lock.yaml");
    const unrelated = join(root, "unrelated.yaml");
    if (process.platform !== "win32") {
      await writeFile(unrelated, "unchanged\n");
      await rm(staged);
      await symlink(unrelated, staged);
    }
    await rm(selected);
    await writeFile(join(eng, "emitter-package-lock.json"), '{"lockfileVersion":3}');
    await syncCommand({ "output-dir": output, "local-spec-repo": spec });
    await expect(lstat(staged)).rejects.toMatchObject({ code: "ENOENT" });
    if (process.platform !== "win32") expect(await readFile(unrelated, "utf8")).toBe("unchanged\n");
    expect(await readFile(join(output, "TempTypeSpecFiles", "package-lock.json"), "utf8")).toBe(
      '{"lockfileVersion":3}',
    );
  });

  it.skipIf(process.platform === "win32")(
    "rejects both valid and dangling pnpm lock symlinks",
    async () => {
      root = await mkdtemp(join(tmpdir(), "tsp-pnpm-link-"));
      const target = join(root, "lock-target");
      const lock = join(root, "lock-link");
      await writeFile(target, "lockfileVersion: '9.0'\n");
      await symlink(target, lock);
      await expect(pnpm.pnpmLockExists(lock)).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
      await expect(pnpmWorkspaceContent(lock)).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
      await rm(target);
      await expect(pnpm.pnpmLockExists(lock)).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
    },
  );

  it.skipIf(process.platform === "win32")(
    "rejects valid and dangling workspace policy symlinks",
    async () => {
      root = await mkdtemp(join(tmpdir(), "tsp-pnpm-sidecar-link-"));
      const manifest = join(root, "emitter.json");
      const target = join(root, "policy.yaml");
      await writeFile(manifest, "{}");
      await writeFile(target, "overrides:\n  example: 1.0.0\n");
      await symlink(target, join(root, "emitter-pnpm-workspace.yaml"));
      await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
      await rm(target);
      await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
    },
  );

  it("selects a custom emitter pnpm lock over the npm lock and stages its workspace policy", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-custom-"));
    const eng = join(root, "eng");
    const output = join(root, "sdk");
    const spec = join(root, "spec");
    await Promise.all([eng, output, spec].map((path) => mkdir(path)));
    await writeFile(join(eng, "custom.json"), '{"dependencies":{}}');
    await writeFile(join(eng, "custom-lock.json"), '{"lockfileVersion":3}');
    await writeFile(join(eng, "custom-pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
    await writeFile(join(eng, "custom-pnpm-workspace.yaml"), "overrides:\n  example: 1.0.0\n");
    await writeFile(join(spec, "main.tsp"), "namespace Example;\n");
    await writeFile(
      join(output, "tsp-location.yaml"),
      "directory: spec\ncommit: test\nrepo: Azure/test\nadditionalDirectories: []\nemitterPackageJsonPath: eng/custom.json\n",
    );
    vi.mocked(getRepoRoot).mockResolvedValue(root);
    await syncCommand({ "output-dir": output, "local-spec-repo": spec });
    const staged = join(output, "TempTypeSpecFiles");
    expect(await readFile(join(staged, "pnpm-lock.yaml"), "utf8")).toContain("lockfileVersion");
    expect(await readFile(join(staged, "pnpm-workspace.yaml"), "utf8")).toContain("example: 1.0.0");
    await expect(readFile(join(staged, "package-lock.json"))).rejects.toMatchObject({
      code: "ENOENT",
    });
  });

  it("preserves pnpm workspace policies and rejects npm overrides and multi-project workspaces", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-policy-"));
    const manifest = join(root, "custom.json");
    await writeFile(manifest, JSON.stringify({ dependencies: {} }));
    await writeFile(join(root, "custom-pnpm-workspace.yaml"), "overrides:\n  example: 1.0.0\n");
    expect(await pnpmWorkspaceContent(manifest)).toContain("example: 1.0.0");
    await writeFile(join(root, "custom-pnpm-workspace.yaml"), "packages:\n  - '../outside'\n");
    await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow(
      "cannot include additional projects",
    );
    await writeFile(manifest, JSON.stringify({ overrides: { example: "1.0.0" } }));
    await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow("npm package.json overrides");
  });

  it.each([
    { packageManager: "pnpm@12.0.0" },
    { packageManager: "npm@12.0.0" },
    { devEngines: { packageManager: { name: "pnpm", version: "12.0.0" } } },
  ])("rejects emitter package manager pins: %j", async (config) => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-pin-"));
    const manifest = join(root, "emitter.json");
    await writeFile(manifest, JSON.stringify(config));
    await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow(
      "tsp-client supplies its own pnpm version",
    );
  });

  it.each([false, true])(
    "does not mask the lock generation outcome when scratch cleanup fails (resolution fails: %s)",
    async (rejectInstall) => {
      root = await mkdtemp(join(tmpdir(), "tsp-pnpm-cleanup-"));
      const manifest = join(root, "emitter.json");
      await writeFile(manifest, "{}");
      let scratch: string | undefined;
      vi.spyOn(workspaceFs, "removeDirectory").mockImplementation(async (path) => {
        scratch = path;
        throw new Error("scratch in use");
      });
      if (rejectInstall)
        vi.spyOn(pnpm, "pnpmCommand").mockRejectedValue(new Error("resolution failed"));
      const lock = join(root, "emitter-pnpm-lock.yaml");
      try {
        if (rejectInstall) {
          await expect(generateLockFileCommandCore(root, manifest, [], "pnpm")).rejects.toThrow(
            "resolution failed",
          );
          await expect(readFile(lock)).rejects.toMatchObject({ code: "ENOENT" });
        } else {
          await generateLockFileCommandCore(root, manifest, [], "pnpm");
          expect(await readFile(lock, "utf8")).toContain("lockfileVersion:");
        }
      } finally {
        if (scratch) await rm(scratch, { force: true, recursive: true });
      }
    },
  );

  it("does not publish a lock if resolution adds an unstaged policy setting", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-policy-change-"));
    const manifest = join(root, "emitter.json");
    await writeFile(manifest, "{}");
    vi.spyOn(pnpm, "pnpmCommand").mockImplementation(async (dir) => {
      const policy = join(dir, "pnpm-workspace.yaml");
      await writeFile(
        policy,
        `${await readFile(policy, "utf8")}minimumReleaseAgeExclude:\n  - fixture@1.0.0\n`,
      );
      await writeFile(join(dir, "pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
    });
    await expect(generateLockFileCommandCore(root, manifest, [], "pnpm")).rejects.toThrow(
      "pnpm changed the workspace policy",
    );
    await expect(readFile(join(root, "emitter-pnpm-lock.yaml"))).rejects.toMatchObject({
      code: "ENOENT",
    });
  });

  it("generates a committed pnpm lock and refuses a frozen manifest mismatch", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-generate-"));
    const manifest = join(root, "custom-emitter.json");
    await writeFile(manifest, JSON.stringify({ name: "example", version: "1.0.0" }));
    await generateLockFileCommandCore(root, manifest, [], "pnpm");
    const lock = await readFile(join(root, "custom-emitter-pnpm-lock.yaml"), "utf8");
    expect(lock).toContain("lockfileVersion:");
    const workspace = join(root, "frozen");
    await mkdir(workspace);
    await writeFile(join(workspace, "pnpm-workspace.yaml"), await pnpmWorkspaceContent(manifest));
    await writeFile(join(workspace, "pnpm-lock.yaml"), lock);
    await writeFile(
      join(workspace, "package.json"),
      JSON.stringify({ dependencies: { "is-number": "7.0.0" } }),
    );
    await expect(pnpmCommand(workspace, ["install", "--frozen-lockfile"])).rejects.toThrow(
      "pnpm install failed",
    );
  }, 30000);

  it("applies a custom pnpm workspace override when generating and installing a lock", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-override-"));
    const manifest = join(root, "custom.json");
    await writeFile(
      manifest,
      JSON.stringify({ name: "override-example", dependencies: { "is-number": "7.0.0" } }),
    );
    await writeFile(join(root, "custom-pnpm-workspace.yaml"), "overrides:\n  is-number: 6.0.0\n");
    await generateLockFileCommand({
      "output-dir": root,
      "emitter-package-json-path": manifest,
      "package-manager": "pnpm",
    });
    const lock = await readFile(join(root, "custom-pnpm-lock.yaml"), "utf8");
    expect(lock).toContain("is-number@6.0.0");
    const installDir = join(root, "install");
    await mkdir(installDir);
    await cp(manifest, join(installDir, "package.json"));
    await writeFile(join(installDir, "pnpm-workspace.yaml"), await pnpmWorkspaceContent(manifest));
    await writeFile(join(installDir, "pnpm-lock.yaml"), lock);
    await pnpmCommand(installDir, ["install", "--frozen-lockfile"]);
    const installed = JSON.parse(
      await readFile(join(installDir, "node_modules/is-number/package.json"), "utf8"),
    );
    expect(installed.version).toBe("6.0.0");
  }, 30000);

  it("generates and uses a standalone committed lock without global pnpm", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-standalone-"));
    const eng = join(root, "eng");
    const output = join(root, "lock-output");
    const destination = join(root, "destination");
    await Promise.all([eng, output, destination].map((path) => mkdir(path)));
    const manifest = join(eng, "emitter-package.json");
    await writeFile(manifest, JSON.stringify({ name: "empty-emitter", version: "1.0.0" }));
    vi.mocked(getRepoRoot).mockResolvedValue(root);
    await generateLockFileCommand({ "output-dir": output, "package-manager": "pnpm" });
    expect(await readFile(join(eng, "emitter-package-pnpm-lock.yaml"), "utf8")).toContain(
      "lockfileVersion:",
    );
    await installDependencies({ path: destination });
    expect(await readFile(join(destination, "pnpm-workspace.yaml"), "utf8")).toContain(
      "strictDepBuilds: true",
    );
    expect(await readFile(join(destination, "pnpm-lock.yaml"), "utf8")).toContain(
      "lockfileVersion:",
    );
  }, 30000);

  it("rejects npm-only options before changing a pnpm emitter manifest", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-args-"));
    const manifest = join(root, "emitter.json");
    const source = join(root, "source.json");
    const original = JSON.stringify({ dependencies: { emitter: "1.0.0" } });
    await writeFile(manifest, original);
    await writeFile(source, JSON.stringify({ name: "emitter", version: "2.0.0" }));
    vi.mocked(getRepoRoot).mockResolvedValue(root);
    await expect(
      generateConfigFilesCommand({
        "output-dir": root,
        "package-json": source,
        "emitter-package-json-path": manifest,
        "package-manager": "pnpm",
        "npm-args": "--legacy-peer-deps",
      }),
    ).rejects.toThrow("--npm-args cannot be used with pnpm");
    expect(await readFile(manifest, "utf8")).toBe(original);
    await rm(manifest);
    await writeFile(
      join(root, "emitter-pnpm-workspace.yaml"),
      "patchedDependencies:\n  example@1.0.0: patches/example.patch\n",
    );
    await expect(
      generateConfigFilesCommand({
        "output-dir": root,
        "package-json": source,
        "emitter-package-json-path": manifest,
        "package-manager": "pnpm",
      }),
    ).rejects.toThrow("Unsupported emitter pnpm workspace setting");
    await expect(readFile(manifest)).rejects.toMatchObject({ code: "ENOENT" });
    await expect(readFile(join(root, "emitter-pnpm-lock.yaml"))).rejects.toMatchObject({
      code: "ENOENT",
    });
  });

  it.each([false, true])(
    "restores the prior lock when manifest publication fails (existing lock: %s)",
    async (existingLock) => {
      root = await mkdtemp(join(tmpdir(), "tsp-pnpm-rollback-"));
      const manifest = join(root, "emitter.json");
      const lock = join(root, "emitter-pnpm-lock.yaml");
      const source = join(root, "source.json");
      const original = JSON.stringify({ name: "fixture" });
      await writeFile(manifest, original);
      await writeFile(source, JSON.stringify({ name: "is-number", version: "7.0.0" }));
      vi.mocked(getRepoRoot).mockResolvedValue(root);
      if (existingLock) await generateLockFileCommandCore(root, manifest, [], "pnpm");
      const prior = existingLock ? await readFile(lock, "utf8") : undefined;
      const realPublish = publishEmitterFile;
      vi.spyOn(pnpm, "publishEmitterFile").mockImplementation(async (path, contents) => {
        if (path === manifest) throw new Error("manifest publication failed");
        await realPublish(path, contents);
      });
      await expect(
        generateConfigFilesCommand({
          "output-dir": root,
          "package-json": source,
          "emitter-package-json-path": manifest,
          "package-manager": "pnpm",
        }),
      ).rejects.toThrow("manifest publication failed");
      expect(await readFile(manifest, "utf8")).toBe(original);
      if (prior === undefined)
        await expect(readFile(lock)).rejects.toMatchObject({ code: "ENOENT" });
      else expect(await readFile(lock, "utf8")).toBe(prior);
    },
    30000,
  );

  it("fails on unapproved install scripts and reruns approved scripts with a warm store", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-scripts-"));
    const dependency = join(root, "dependency");
    const first = join(root, "first");
    const second = join(root, "second");
    const counter = join(root, "counter");
    await Promise.all([dependency, first, second].map((path) => mkdir(path)));
    await writeFile(
      join(dependency, "package.json"),
      JSON.stringify({
        name: "test-build-script",
        version: "1.0.0",
        scripts: { install: "node install.cjs" },
      }),
    );
    await writeFile(
      join(dependency, "install.cjs"),
      `require('node:fs').appendFileSync(${JSON.stringify(counter)}, '1');`,
    );
    const manifest = join(root, "emitter.json");
    const contents = JSON.stringify({
      name: "fixture",
      dependencies: { "test-build-script": "file:../dependency" },
    });
    await writeFile(manifest, contents);
    await expect(pnpmWorkspaceContent(manifest)).rejects.toThrow("Local dependency reference");
    // The policy smoke test supplies its own local dependency in each test workspace.
    await writeFile(manifest, JSON.stringify({ name: "fixture" }));
    for (const dir of [first, second]) await writeFile(join(dir, "package.json"), contents);
    await writeFile(join(first, "pnpm-workspace.yaml"), await pnpmWorkspaceContent(manifest));
    const args = ["install", "--store-dir", join(root, "store")];
    await expect(pnpmCommand(first, args)).rejects.toThrow("pnpm install failed");
    await expect(readFile(counter)).rejects.toMatchObject({ code: "ENOENT" });
    await writeFile(
      join(root, "emitter-pnpm-workspace.yaml"),
      "allowBuilds:\n  'test-build-script@file:../dependency': true\n",
    );
    for (const dir of [first, second]) {
      await writeFile(join(dir, "pnpm-workspace.yaml"), await pnpmWorkspaceContent(manifest));
      await pnpmCommand(dir, args);
    }
    expect(await readFile(counter, "utf8")).toBe("11");
  }, 30000);

  it("installs a committed pnpm lock and generates the real TypeScript emitter fixture", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-e2e-"));
    const eng = join(root, "eng");
    const output = join(root, "output");
    const localSpec = join(root, "spec");
    const fixture = fileURLToPath(new URL("./utils/", import.meta.url));
    const specs = fileURLToPath(
      new URL("./examples/specification/contosowidgetmanager/", import.meta.url),
    );
    await Promise.all([eng, output, localSpec].map((path) => mkdir(path, { recursive: true })));
    await cp(join(fixture, "emitter-package.json"), join(eng, "emitter-package.json"));
    await cp(
      join(fixture, "emitter-package-pnpm-lock.yaml"),
      join(eng, "emitter-package-pnpm-lock.yaml"),
    );
    await cp(join(specs, "Contoso.WidgetManager"), join(localSpec, "Contoso.WidgetManager"), {
      recursive: true,
    });
    await cp(
      join(specs, "Contoso.WidgetManager.Shared"),
      join(localSpec, "Contoso.WidgetManager.Shared"),
      { recursive: true },
    );
    await writeFile(
      join(output, "tsp-location.yaml"),
      "directory: Contoso.WidgetManager\ncommit: test\nrepo: Azure/test\nadditionalDirectories:\n  - spec/Contoso.WidgetManager.Shared\n",
    );
    vi.mocked(getRepoRoot).mockResolvedValue(root);
    vi.spyOn(process, "exit").mockImplementation(((code?: number) => {
      throw new Error(`process.exit(${code})`);
    }) as typeof process.exit);
    await syncCommand({
      "output-dir": output,
      "local-spec-repo": join(localSpec, "Contoso.WidgetManager"),
    });
    if (process.platform !== "win32") {
      const manifest = join(eng, "emitter-package.json");
      const contents = await readFile(manifest, "utf8");
      const invalid = join(eng, "invalid.json");
      await writeFile(invalid, "not JSON");
      await rm(manifest);
      await symlink(invalid, manifest);
      await expect(generateCommand({ "output-dir": output })).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
      await rm(manifest);
      await writeFile(manifest, contents);
    }
    const sidecar = join(eng, "emitter-package-pnpm-workspace.yaml");
    await writeFile(sidecar, "allowBuilds:\n  test-build-script: false\n");
    await expect(generateCommand({ "output-dir": output })).rejects.toThrow("run tsp-client sync");
    await rm(sidecar);
    const selectedLock = join(eng, "emitter-package-pnpm-lock.yaml");
    const lockContents = await readFile(selectedLock, "utf8");
    await writeFile(selectedLock, `${lockContents}\n# refreshed\n`);
    await expect(generateCommand({ "output-dir": output })).rejects.toThrow("run tsp-client sync");
    await rm(selectedLock);
    await expect(generateCommand({ "output-dir": output })).rejects.toThrow(
      "package manager changed",
    );
    await writeFile(selectedLock, lockContents);
    await generateCommand({ "output-dir": output });
    const generated = join(root, "sdk/contosowidgetmanager/contosowidgetmanager-rest");
    expect((await stat(join(generated, "src/index.ts"))).isFile()).toBe(true);
    const pnpmOutput = await readFiles(generated);
    await rm(generated, { recursive: true });
    await rm(join(eng, "emitter-package-pnpm-lock.yaml"));
    await cp(join(fixture, "emitter-package-lock.json"), join(eng, "emitter-package-lock.json"));
    await syncCommand({
      "output-dir": output,
      "local-spec-repo": join(localSpec, "Contoso.WidgetManager"),
    });
    await generateCommand({ "output-dir": output });
    expect(await readFiles(generated)).toEqual(pnpmOutput);
  }, 120000);

  it.skipIf(process.platform === "win32")(
    "does not follow a dangling standalone pnpm workspace symlink",
    async () => {
      root = await mkdtemp(join(tmpdir(), "tsp-pnpm-destination-link-"));
      const eng = join(root, "eng");
      const destination = join(root, "destination");
      const target = join(root, "outside");
      await Promise.all([mkdir(eng), mkdir(destination)]);
      await writeFile(join(eng, "emitter-package.json"), "{}");
      await writeFile(join(eng, "emitter-package-pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
      await symlink(target, join(destination, "pnpm-workspace.yaml"));
      vi.mocked(getRepoRoot).mockResolvedValue(root);
      await expect(installDependencies({ path: destination })).rejects.toThrow(
        "Expected a regular emitter lockfile",
      );
      await expect(readFile(target)).rejects.toMatchObject({ code: "ENOENT" });
      await expect(readFile(join(destination, "package.json"))).rejects.toMatchObject({
        code: "ENOENT",
      });
    },
  );

  it("does not overwrite a different existing standalone pnpm workspace", async () => {
    root = await mkdtemp(join(tmpdir(), "tsp-pnpm-conflict-"));
    const eng = join(root, "eng");
    const destination = join(root, "destination");
    await mkdir(eng);
    await mkdir(destination);
    await writeFile(join(eng, "emitter-package.json"), JSON.stringify({ dependencies: {} }));
    await writeFile(join(eng, "emitter-package-pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
    await writeFile(join(destination, "pnpm-workspace.yaml"), "packages:\n  - existing\n");
    vi.mocked(getRepoRoot).mockResolvedValue(root);
    await expect(installDependencies({ path: destination })).rejects.toThrow(
      "Refusing to overwrite",
    );
    expect(await readFile(join(destination, "pnpm-workspace.yaml"), "utf8")).toBe(
      "packages:\n  - existing\n",
    );
    await expect(readFile(join(destination, "package.json"))).rejects.toMatchObject({
      code: "ENOENT",
    });
  });
});
