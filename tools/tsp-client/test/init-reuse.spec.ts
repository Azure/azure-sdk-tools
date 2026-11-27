import { execFileSync } from "node:child_process";
import { chmod, mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { compile, NodeHost } from "@typespec/compiler";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { generateCommand, initCommand, syncCommand } from "../src/commands.js";
import * as gitHelpers from "../src/git.js";

vi.mock("../src/typespec.js", async (original) => ({
  ...(await original<typeof import("../src/typespec.js")>()),
  compileTsp: async ({
    resolvedMainFilePath,
    outputPath,
  }: {
    resolvedMainFilePath: string;
    outputPath: string;
  }) => {
    const program = await compile(NodeHost, resolvedMainFilePath, { noEmit: true });
    expect(program.diagnostics).toEqual([]);
    const models = [...program.getGlobalNamespaceType().models.keys()].sort();
    await writeFile(join(outputPath, "models.json"), JSON.stringify(models));
    return { success: true, exampleCmd: "" };
  },
}));

const remote = "https://github.com/Test/azure-rest-api-specs";
let root: string;
let source: string;
let sdk: string;
let commit: string;
let trace: string;

function git(cwd: string, ...args: string[]): string {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

beforeEach(async () => {
  root = await mkdtemp(join(tmpdir(), "tsp-init-reuse-"));
  source = join(root, "source");
  sdk = join(root, "sdk");
  trace = join(root, "git.trace");
  await mkdir(source);
  await mkdir(join(sdk, "eng"), { recursive: true });
  const config = join(root, "git-config");
  const template = join(root, "template");
  await writeFile(config, "");
  await mkdir(template);
  vi.stubEnv("GIT_CONFIG_GLOBAL", config);
  vi.stubEnv("GIT_CONFIG_NOSYSTEM", "1");
  for (const name of [
    "GIT_CONFIG",
    "GIT_CONFIG_PARAMETERS",
    "GIT_DIR",
    "GIT_COMMON_DIR",
    "GIT_WORK_TREE",
    "GIT_INDEX_FILE",
    "GIT_OBJECT_DIRECTORY",
    "GIT_ALTERNATE_OBJECT_DIRECTORIES",
    "GIT_ATTR_SOURCE",
  ])
    vi.stubEnv(name, undefined);
  vi.stubEnv("GIT_CONFIG_COUNT", "0");
  vi.stubEnv("GIT_TEMPLATE_DIR", template);
  vi.stubEnv("GIT_DEFAULT_HASH", "sha1");
  vi.stubEnv("GIT_ATTR_NOSYSTEM", "1");
  vi.stubEnv("XDG_CONFIG_HOME", join(root, "xdg"));
  git(sdk, "init", "-q");
  git(source, "init", "-q", "-b", "main");
  git(source, "config", "user.name", "Test");
  git(source, "config", "user.email", "test@example.invalid");
  git(source, "config", "uploadpack.allowFilter", "true");
  git(source, "config", "core.autocrlf", "false");
  await mkdir(join(source, "service/spec/nested"), { recursive: true });
  await mkdir(join(source, "common/extra"), { recursive: true });
  await writeFile(
    join(source, "service/spec/main.tsp"),
    'import "../extra/helper.tsp";\nimport "./nested/model.tsp";\n',
  );
  await writeFile(join(source, "service/spec/nested/model.tsp"), "model Feature {}\n");
  await writeFile(join(source, "common/extra/helper.tsp"), "model Helper {}\n");
  await writeFile(
    join(source, "service/spec/tspconfig.yaml"),
    `options:
  "@typespec/http-test":
    service-dir: generated
    package-dir: package
  "@azure-tools/typespec-client-generator-cli":
    additionalDirectories:
      - common/extra
`,
  );
  await writeFile(
    join(sdk, "eng/emitter-package.json"),
    '{"dependencies":{"@typespec/http-test":"1.0.0"}}',
  );
  git(source, "add", ".");
  git(source, "commit", "-qm", "fixture");
  commit = git(source, "rev-parse", "HEAD");
  vi.stubEnv("GIT_CONFIG_COUNT", "1");
  vi.stubEnv("GIT_CONFIG_KEY_0", `url.${pathToFileURL(source).href}.insteadOf`);
  vi.stubEnv("GIT_CONFIG_VALUE_0", `${remote}.git`);
  vi.stubEnv("GIT_TRACE2_EVENT", trace);
});

afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  await rm(root, { recursive: true, force: true });
});

async function init(options: Record<string, boolean> = {}) {
  return initCommand({
    "output-dir": sdk,
    "tsp-config": `${remote}/blob/${commit}/service/spec/tspconfig.yaml`,
    "skip-install": true,
    "save-inputs": true,
    ...options,
  });
}

async function gitStarts() {
  return (await readFile(trace, "utf8"))
    .trim()
    .split("\n")
    .map((row) => JSON.parse(row))
    .filter((event) => event.event === "start");
}

async function clones() {
  return (await gitStarts()).filter((event) => event.argv[1] === "clone").length;
}

it("uses one clone for remote init and preserves the flattened workspace", async () => {
  const output = await init();
  const inputs = join(output, "TempTypeSpecFiles");
  expect(await readFile(join(inputs, "spec/main.tsp"), "utf8")).toContain("../extra/helper.tsp");
  expect(await readFile(join(inputs, "spec/nested/model.tsp"), "utf8")).toBe("model Feature {}\n");
  expect(await readFile(join(inputs, "extra/helper.tsp"), "utf8")).toBe("model Helper {}\n");
  expect((await readdir(inputs)).sort()).toEqual(["extra", "package.json", "spec"]);
  expect((await readdir(root)).filter((entry) => entry.startsWith("sparse-spec"))).toEqual([]);
  expect(await clones()).toBe(1);
  const starts = await gitStarts();
  const adds = starts.filter(
    (event) => event.argv[1] === "sparse-checkout" && event.argv[2] === "add",
  );
  expect(adds.map((event) => event.argv.slice(3))).toEqual([
    ["service/spec/tspconfig.yaml"],
    ["service/spec", "common/extra"],
  ]);
});

it("does not reuse the init checkout in a separate sync", async () => {
  const output = await init();
  const generated = await readFile(join(output, "models.json"), "utf8");
  expect(generated).toBe('["Feature","Helper"]');
  await rm(join(output, "TempTypeSpecFiles"), { recursive: true });
  await syncCommand({ "output-dir": output });
  await generateCommand({ "output-dir": output, "skip-install": true });
  expect(await readFile(join(output, "models.json"), "utf8")).toBe(generated);
  expect(await clones()).toBe(2);
});

it("falls back to an independent clone when expansion fails", async () => {
  const add = gitHelpers.addSpecFiles;
  vi.spyOn(gitHelpers, "addSpecFiles")
    .mockImplementationOnce(add)
    .mockImplementationOnce(async (cloneDir, project) => {
      await add(cloneDir, project!);
      throw new Error("simulated failure after partial sparse expansion");
    });
  const clone = gitHelpers.cloneRepo;
  vi.spyOn(gitHelpers, "cloneRepo")
    .mockImplementationOnce(clone)
    .mockImplementationOnce(async (...args) => {
      expect((await readdir(root)).filter((entry) => entry.startsWith("sparse-spec"))).toHaveLength(
        1,
      );
      await clone(...args);
    });
  const output = await init();
  expect(await readFile(join(output, "TempTypeSpecFiles/extra/helper.tsp"), "utf8")).toBe(
    "model Helper {}\n",
  );
  expect(await clones()).toBe(2);
  expect((await readdir(root)).filter((entry) => entry.startsWith("sparse-spec"))).toEqual([]);
});

it.skipIf(process.platform === "win32").each([false, true])(
  "keeps branch-dependent checkout settings (GIT_CONFIG override: %s)",
  async (override) => {
    if (override) {
      const empty = join(root, "probe-config");
      await writeFile(empty, "");
      vi.stubEnv("GIT_CONFIG", empty);
    }
    const checkoutConfig = join(root, "main.gitconfig");
    await writeFile(
      process.env.GIT_CONFIG_GLOBAL!,
      `[includeIf "onbranch:main"]\n  path = ${checkoutConfig}\n`,
    );
    await writeFile(checkoutConfig, "[core]\n  autocrlf = true\n");
    const output = await init();
    expect(await readFile(join(output, "TempTypeSpecFiles/spec/nested/model.tsp"), "utf8")).toBe(
      "model Feature {}\r\n",
    );
    expect(await clones()).toBe(2);
  },
);

it("falls back when Git has a custom hook location", async () => {
  const hooks = join(root, "custom-hooks");
  await mkdir(hooks);
  await writeFile(
    process.env.GIT_CONFIG_GLOBAL!,
    `[core]\n hooksPath = "${hooks.replaceAll("\\", "/")}"\n`,
  );
  const output = await init();
  expect(await readFile(join(output, "models.json"), "utf8")).toBe('["Feature","Helper"]');
  expect(await clones()).toBe(2);
});

it.skipIf(process.platform === "win32")(
  "preserves fresh bytes with worktree-relative attributes",
  async () => {
    await writeFile(join(source, "checkout-attributes"), "common/extra/*.tsp text eol=crlf\n");
    git(source, "add", "checkout-attributes");
    git(source, "commit", "-qm", "checkout attributes");
    commit = git(source, "rev-parse", "HEAD");
    await writeFile(
      process.env.GIT_CONFIG_GLOBAL!,
      "[core]\n attributesFile = checkout-attributes\n",
    );
    const output = await init();
    expect(await readFile(join(output, "TempTypeSpecFiles/extra/helper.tsp"), "utf8")).toBe(
      "model Helper {}\n",
    );
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "does not reuse checkout for a cwd-relative mirror",
  async () => {
    vi.stubEnv("GIT_CONFIG_KEY_0", "url.../source.insteadOf");
    await expect(init()).rejects.toThrow(/git clone failed/);
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "falls back with a template-installed index hook",
  async () => {
    const hooks = join(process.env.GIT_TEMPLATE_DIR!, "hooks");
    await mkdir(hooks);
    const hook = join(hooks, "post-index-change");
    await writeFile(hook, "#!/bin/sh\nexit 0\n");
    await chmod(hook, 0o755);
    await init();
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "falls back with a template-installed reference transaction hook",
  async () => {
    const hooks = join(process.env.GIT_TEMPLATE_DIR!, "hooks");
    await mkdir(hooks);
    const hook = join(hooks, "reference-transaction");
    await writeFile(hook, "#!/bin/sh\nexit 0\n");
    await chmod(hook, 0o755);
    await init();
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "does not reuse when a Git config path is cwd-relative",
  async () => {
    vi.stubEnv("GIT_CONFIG_COUNT", "0");
    vi.stubEnv("GIT_CONFIG_GLOBAL", "../mirror.gitconfig");
    const mirrorConfig = (url: string) => `[url "${url}"]\n insteadOf = ${remote}.git\n`;
    await writeFile(join(root, "mirror.gitconfig"), mirrorConfig(pathToFileURL(source).href));
    const packageDir = join(sdk, "generated/package");
    await mkdir(packageDir, { recursive: true });
    await writeFile(
      join(packageDir, "mirror.gitconfig"),
      mirrorConfig(pathToFileURL(join(root, "missing")).href),
    );
    await expect(init()).rejects.toThrow(/git clone failed/);
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "does not probe a preconfigured origin instead of the clone remote",
  async () => {
    vi.stubEnv("GIT_CONFIG_KEY_0", "url.../source.insteadOf");
    await writeFile(process.env.GIT_CONFIG_GLOBAL!, "[clone]\n defaultRemoteName = mirror\n");
    await writeFile(
      join(process.env.GIT_TEMPLATE_DIR!, "config"),
      `[remote "origin"]\n url = ${pathToFileURL(source).href}\n`,
    );
    const add = vi.spyOn(gitHelpers, "addSpecFiles");
    await expect(init()).rejects.toThrow(/git clone failed/);
    expect(add).toHaveBeenCalledTimes(1);
    expect(await clones()).toBe(2);
  },
);

it.skipIf(process.platform === "win32")(
  "preserves fresh bytes with a config include materialized by checkout",
  async () => {
    const includedConfig = "checkout.gitconfig";
    const includedPath = join(source, includedConfig);
    await writeFile(
      join(process.env.GIT_TEMPLATE_DIR!, "config"),
      `[include]\n path = ../${includedConfig}\n`,
    );
    await writeFile(includedPath, "[core]\n autocrlf = true\n");
    git(source, "add", includedConfig);
    git(source, "commit", "-qm", "pinned checkout config");
    commit = git(source, "rev-parse", "HEAD");
    git(source, "rm", includedConfig);
    git(source, "commit", "-qm", "default tip without checkout config");
    const output = await init();
    const inputs = join(output, "TempTypeSpecFiles");
    const viaInit = await readFile(join(inputs, "extra/helper.tsp"));
    await rm(inputs, { recursive: true });
    await syncCommand({ "output-dir": output });
    expect(viaInit).toEqual(await readFile(join(inputs, "extra/helper.tsp")));
    expect(viaInit.toString()).toBe("model Helper {}\n");
    expect(await clones()).toBe(3);
  },
);

it("does not sync when init skips generation", async () => {
  const output = await init({ "skip-sync-and-generate": true });
  expect(await readdir(output)).toEqual(["tsp-location.yaml"]);
  expect(await clones()).toBe(1);
});
