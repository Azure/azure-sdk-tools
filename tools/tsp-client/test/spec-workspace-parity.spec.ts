import { execFileSync } from "node:child_process";
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { compile, NodeHost, normalizePath } from "@typespec/compiler";
import { afterEach, expect, it, vi } from "vitest";
import { syncCommand } from "../src/commands.js";
import { discoverEntrypointFile } from "../src/typespec.js";

let root: string;
function git(cwd: string, ...args: string[]) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

afterEach(async () => {
  vi.unstubAllEnvs();
  if (root) await rm(root, { recursive: true, force: true });
});

it("preserves full local and remote workspaces, diagnostics, and emitted resources", async () => {
  root = await mkdtemp(join(tmpdir(), "tsp-spec-parity-"));
  const source = join(root, "source");
  const project = "specification/service/project";
  const shared = "specification/common/shared";
  const sdk = join(root, "repo");
  const gitConfig = join(root, "gitconfig");
  const excludes = join(root, "global-ignore");
  await writeFile(excludes, "*.json\n");
  await writeFile(
    gitConfig,
    `[core]\n autocrlf = false\n excludesFile = "${normalizePath(excludes)}"\n`,
  );
  vi.stubEnv("GIT_CONFIG_GLOBAL", gitConfig);
  vi.stubEnv("GIT_CONFIG_NOSYSTEM", "1");
  vi.stubEnv("GIT_CONFIG_COUNT", "0");
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
  ]) {
    vi.stubEnv(name, undefined);
  }
  const template = join(root, "git-template");
  await mkdir(template);
  vi.stubEnv("GIT_TEMPLATE_DIR", template);
  vi.stubEnv("GIT_DEFAULT_HASH", "sha1");
  await mkdir(join(source, project, "nested"), { recursive: true });
  await mkdir(join(source, project, "examples"));
  await mkdir(join(source, project, "historical", "2020"), { recursive: true });
  await mkdir(join(source, shared), { recursive: true });
  await mkdir(join(sdk, "eng"), { recursive: true });
  git(source, "init", "-q");
  git(sdk, "init", "-q");
  git(source, "config", "uploadpack.allowFilter", "true");
  await writeFile(
    join(source, project, "nested/entry.tsp"),
    'import "../../shared/helper.tsp";\nmodel Example { value: Helper; }\n',
  );
  await writeFile(join(source, shared, "helper.tsp"), "model Helper { value: string; }\n");
  await writeFile(join(source, project, "examples/example.json"), '{"example":"preserve me"}\n');
  await writeFile(join(source, project, "historical/2020/response.json"), '{"historical":true}\n');
  await writeFile(
    join(source, project, "tspconfig.yaml"),
    "parameters:\n  resource:\n    default: examples/example.json\n",
  );
  await writeFile(join(sdk, "eng/emitter-package.json"), '{"dependencies":{}}\n');
  await writeFile(
    join(sdk, "eng/emitter-package-lock.json"),
    '{"lockfileVersion":3,"packages":{}}\n',
  );
  // Commit every fixture file even when a developer's Git ignores JSON files.
  git(source, "add", "--force", ".");
  git(
    source,
    "-c",
    "user.name=Test",
    "-c",
    "user.email=test@example.invalid",
    "commit",
    "-qm",
    "fixture",
  );
  const commit = git(source, "rev-parse", "HEAD");
  const url = "https://github.com/Test/tsp-spec-fixture.git";
  vi.stubEnv("GIT_CONFIG_COUNT", "1");
  vi.stubEnv("GIT_CONFIG_KEY_0", `url.${pathToFileURL(source).href}.insteadOf`);
  vi.stubEnv("GIT_CONFIG_VALUE_0", url);

  const emitter = join(root, "emitter.mjs");
  await writeFile(
    emitter,
    `
    import { readFile, mkdir, writeFile } from "node:fs/promises";
    import { dirname, join } from "node:path";
    export async function $onEmit({ program, emitterOutputDir }) {
      const script = Array.from(program.sourceFiles.values()).find(node => node.file.path.endsWith("/nested/entry.tsp"));
      const spec = dirname(dirname(script.file.path));
      const example = await readFile(join(spec, "examples/example.json"), "utf8");
      const config = await readFile(join(spec, "tspconfig.yaml"), "utf8");
      const models = [...program.getGlobalNamespaceType().models.keys()].sort();
      await mkdir(emitterOutputDir, { recursive: true });
      await writeFile(join(emitterOutputDir, "generated.json"), JSON.stringify({ example, config, models }));
    }
  `,
  );

  async function stage(kind: "local" | "remote") {
    const output = join(sdk, kind);
    await mkdir(output);
    await writeFile(
      join(output, "tsp-location.yaml"),
      `repo: Test/tsp-spec-fixture\ncommit: ${commit}\ndirectory: ${project}\nadditionalDirectories:\n  - ${shared}\n`,
    );
    await syncCommand({
      "output-dir": output,
      "local-spec-repo": kind === "local" ? join(source, project) : undefined,
    });
    return join(output, "TempTypeSpecFiles");
  }

  async function tree(path: string) {
    const files: [string, string][] = [];
    for (const name of (await readdir(path, { recursive: true, withFileTypes: true }))
      .filter((entry) => entry.isFile())
      .map((entry) => normalizePath(join(entry.parentPath, entry.name).slice(path.length + 1)))
      .sort()) {
      files.push([name, await readFile(join(path, name), "utf8")]);
    }
    return files;
  }

  async function emit(workspace: string, method: "recursive" | "targeted") {
    const spec = join(workspace, "project");
    const names =
      method === "recursive" ? (await readdir(spec, { recursive: true })).map(normalizePath) : [];
    const selected =
      method === "recursive"
        ? names.find((name) => name === "nested/entry.tsp")
        : await discoverEntrypointFile(spec, "nested\\entry.tsp");
    expect(selected).toBeDefined();
    const outputDir = join(workspace, "emitted");
    const program = await compile(NodeHost, join(spec, selected!), {
      emit: [normalizePath(emitter)],
      outputDir,
    });
    expect(program.diagnostics).toEqual([]);
    return readFile(join(outputDir, "generated.json"));
  }

  const local = await stage("local");
  const remote = await stage("remote");
  const original = await tree(local);
  expect(await tree(remote)).toEqual(original);
  expect(original.map(([name]) => name)).toContain("project/historical/2020/response.json");
  const expected = await emit(local, "recursive");
  const fixture = JSON.parse(expected.toString());
  expect(fixture.models).toEqual(["Example", "Helper"]);
  expect(JSON.parse(fixture.example)).toEqual({ example: "preserve me" });
  expect(fixture.config).toContain("examples/example.json");
  expect(await emit(remote, "targeted")).toEqual(expected);
  expect((await readdir(root)).filter((name) => name.startsWith("sparse-spec"))).toEqual([]);
});
