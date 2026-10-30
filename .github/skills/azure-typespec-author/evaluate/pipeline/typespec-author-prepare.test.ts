import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";

import { configureSkillEnvironments, prepareBenchmark } from "./typespec-author-prepare.ts";

function createMcpBinary(root: string, kind: "live" | "mock") {
  const file = path.join(root, "artifacts", "mcp", kind === "live" ? "cli" : "mock",
    kind === "live" ? "azsdk.dll" : "azsdk-mock.dll");
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, "");
  return file;
}

function fixture(t: { after: (fn: () => void) => void }, naming = true, required = true) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-skill-config with spaces-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const author = path.join(root, "azure-typespec-author");
  const evalRoot = path.join(author, "evaluate");
  fs.mkdirSync(evalRoot, { recursive: true });
  for (const name of ["azure-typespec-author", ...(naming ? ["azsdk-common-typespec-naming"] : [])]) {
    const dir = path.join(root, name);
    fs.mkdirSync(path.join(dir, "references"), { recursive: true });
    fs.mkdirSync(path.join(dir, "evals"), { recursive: true });
    fs.writeFileSync(path.join(dir, "SKILL.md"), name === "azure-typespec-author" && required
      ? "Depends on azsdk-common-typespec-naming" : name);
    fs.writeFileSync(path.join(dir, "references", "rules.md"), name);
    fs.writeFileSync(path.join(dir, "evals", "secret-grader.yaml"), "must not leak");
  }
  const environments = {
    sdk: {
      skills: ["../..", "../../../azsdk-common-typespec-naming"],
      mcpServers: { sdk: { cwd: "../../.." } },
    },
    "azsdk-mcp": {
      mcpServers: {
        "azure-sdk-mcp": {
          type: "stdio", command: "node", args: ["local-launcher.cjs"], cwd: root,
          timeout: "5m", env: { AZURE_SDK_KB_ENDPOINT: "https://example.test" },
        },
      },
    },
  };
  createMcpBinary(root, "live");
  createMcpBinary(root, "mock");
  return { root, evalRoot, environments, stage: path.join(root, "runtime", "skills") };
}

test("mounts declared definitions and references, never evaluators or fixture dependencies", (t) => {
  const { root, evalRoot, environments, stage } = fixture(t);
  configureSkillEnvironments(environments, evalRoot, false, stage);
  assert.equal(environments.sdk.skills.length, 2);
  for (const skill of environments.sdk.skills) {
    assert.deepEqual(fs.readdirSync(skill).sort(), ["SKILL.md", "references"]);
    assert.equal(fs.readFileSync(path.join(skill, "references", "rules.md"), "utf8"), path.basename(skill));
    assert.ok(path.isAbsolute(skill));
  }
  assert.equal(environments.sdk.mcpServers.sdk.cwd, path.resolve(evalRoot, "../../.."));
  assert.ok(fs.existsSync(path.join(root, "azure-typespec-author", "evals", "secret-grader.yaml")));
});

test("no-skill mode overrides explicit mounts and does not stage source skills", (t) => {
  const { evalRoot, environments, stage } = fixture(t);
  configureSkillEnvironments(environments, evalRoot, true, stage);
  assert.deepEqual(environments.sdk.skills, []);
  assert.equal(fs.existsSync(stage), false);
});

test("a branch predating naming mounts only its own authoring skill", (t) => {
  const { evalRoot, environments, stage } = fixture(t, false, false);
  configureSkillEnvironments(environments, evalRoot, false, stage);
  assert.deepEqual(environments.sdk.skills.map((entry) => path.basename(entry)), ["azure-typespec-author"]);
  assert.equal(fs.existsSync(path.join(stage, "azsdk-common-typespec-naming")), false);
});

test("a missing required naming dependency remains an explicit error", (t) => {
  const { evalRoot, environments, stage } = fixture(t, false, true);
  assert.throws(
    () => configureSkillEnvironments(environments, evalRoot, false, stage),
    /Missing declared benchmark skill: .*azsdk-common-typespec-naming/,
  );
});

test("missing non-naming inputs are not silently omitted", (t) => {
  const { evalRoot, environments, stage } = fixture(t);
  environments.sdk.skills.push("../../../missing-skill");
  assert.throws(
    () => configureSkillEnvironments(environments, evalRoot, false, stage),
    /Missing declared benchmark skill: .*missing-skill/,
  );
});

for (const [mode, kind] of ["forced", "trigger", "no-skill"].flatMap((mode) =>
  ["live", "mock"].map((kind) => [mode, kind]))) {
  test(`${mode}/${kind} uses direct MCP startup, standard selection and isolated skill inputs`, async (t) => {
    const { root, evalRoot, environments, stage } = fixture(t);
    const evalDir = path.join(evalRoot, "evals");
    fs.mkdirSync(evalDir);
    fs.writeFileSync(path.join(evalRoot, ".vally.yaml"), JSON.stringify({ environments }));
    const file = path.join(evalDir, "example.eval.yaml");
    fs.writeFileSync(file, JSON.stringify({
      name: "example", description: "Configuration regression", version: "1.0", type: "capability",
      agent_environment: "sdk",
      defaults: { executor: "copilot-sdk" },
      stimuli: ["forced", "trigger", "no-skill"].map((item) => ({
        name: item, tags: { mode: item }, prompt: "Configuration fixture only",
        graders: [{ type: "file-exists", config: { path: "example.txt" } }],
      })),
    }));
    await prepareBenchmark(evalRoot, mode, stage, kind);
    const requireEval = createRequire(new URL("../../../../../eng/common/scripts/eval/package.json", import.meta.url));
    const { ProjectContext, discoverAndFilterEvals, parseSuiteFilter } =
      await import(pathToFileURL(requireEval.resolve("@microsoft/vally")).href);
    // Vally 0.19 reloads project context from explicit -e paths, not a temporary suite.
    const context = await ProjectContext.load(path.dirname(file));
    assert.equal(context.dir, evalRoot);
    const selected = await discoverAndFilterEvals(context, {
      suiteEvals: [file], filter: parseSuiteFilter([`mode=${mode}`]),
    });
    assert.deepEqual(selected.errors, []);
    assert.equal(selected.matched.length, 1);
    assert.deepEqual(selected.matched[0].stimuli.map((stimulus) => stimulus.name), [mode]);
    const skills = context.config.environments.sdk.skills;
    if (mode === "no-skill") {
      assert.deepEqual(skills, []);
      assert.equal(fs.existsSync(stage), false);
    } else {
      assert.equal(skills.length, 2);
      for (const skill of skills) {
        assert.ok(path.isAbsolute(skill));
        assert.deepEqual(fs.readdirSync(skill).sort(), ["SKILL.md", "references"]);
      }
    }
    assert.equal(context.config.environments.sdk.mcpServers.sdk.cwd, path.resolve(evalRoot, "../../.."));
    const server = context.config.environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"];
    const expectedDll = path.join(root, "artifacts", "mcp", kind === "live" ? "cli" : "mock",
      kind === "live" ? "azsdk.dll" : "azsdk-mock.dll");
    assert.equal(server.command, "dotnet");
    assert.deepEqual(server.args, kind === "live" ? [expectedDll, "mcp"] : [expectedDll]);
    assert.equal(server.cwd, root);
    assert.equal(server.timeout, environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"].timeout);
    assert.deepEqual(server.env, environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"].env);
  });
}

test("invalid MCP kinds and missing binaries fail before writing prepared configuration", async (t) => {
  const { root, evalRoot, environments, stage } = fixture(t);
  const configPath = path.join(evalRoot, ".vally.yaml");
  const original = JSON.stringify({ environments });
  fs.writeFileSync(configPath, original);
  for (const kind of ["", "LIVE", "unknown"]) {
    await assert.rejects(prepareBenchmark(evalRoot, "forced", stage, kind), /AZSDK_EVAL_MCP_KIND must be live or mock/);
  }
  for (const kind of ["live", "mock"] as const) {
    const file = createMcpBinary(root, kind);
    fs.unlinkSync(file);
    await assert.rejects(prepareBenchmark(evalRoot, "forced", stage, kind), /Prebuilt .* MCP binary does not exist/);
    fs.mkdirSync(file);
    await assert.rejects(prepareBenchmark(evalRoot, "forced", stage, kind), /Prebuilt .* MCP binary does not exist or is not a file/);
  }
  assert.equal(fs.readFileSync(configPath, "utf8"), original);
  assert.equal(fs.existsSync(stage), false);
});

test("the prepare CLI reads the pipeline MCP kind and rejects a missing selection", (t) => {
  const { evalRoot, environments, stage } = fixture(t);
  const configPath = path.join(evalRoot, ".vally.yaml");
  fs.writeFileSync(configPath, JSON.stringify({ environments }));
  const args = ["--experimental-strip-types", path.join(import.meta.dirname, "typespec-author-prepare.ts"),
    "--prepare", "--eval-root", evalRoot, "--mode", "no-skill", "--skill-root", stage];
  const env = { ...process.env };
  delete env.AZSDK_EVAL_MCP_KIND;
  const missing = spawnSync(process.execPath, args, { env, encoding: "utf8" });
  assert.ifError(missing.error);
  assert.equal(missing.status, 1);
  assert.match(missing.stderr, /AZSDK_EVAL_MCP_KIND must be live or mock/);
  execFileSync(process.execPath, args, { env: { ...env, AZSDK_EVAL_MCP_KIND: "live" }, stdio: "pipe" });
  const server = JSON.parse(fs.readFileSync(configPath, "utf8")).environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"];
  assert.equal(server.command, "dotnet");
  assert.equal(server.args[1], "mcp");
});

test("benchmark entrypoints use caller-owned auth and common repeated-trial reporting", () => {
  const requireEval = createRequire(new URL("../../../../../eng/common/scripts/eval/package.json", import.meta.url));
  const { parse } = requireEval("yaml");
  for (const file of ["azure-typespec-author-benchmark.yml", "azure-typespec-author-benchmark-no-skill.yml"]) {
    const pipeline = parse(fs.readFileSync(new URL(`../../../../../eng/pipelines/${file}`, import.meta.url), "utf8"));
    const connection = pipeline.parameters.find((parameter: { name: string }) => parameter.name === "azureSubscription");
    assert.equal(connection, undefined, `${file}: connection must not require a run parameter`);
    const runs = pipeline.parameters.find((parameter: { name: string }) => parameter.name === "Runs");
    assert.equal(runs.type, "number");
    const configuredConnection = pipeline.variables.find((variable: { name?: string }) =>
      variable.name === "TypeSpecAuthorEvalAzureSubscription");
    assert.ok(configuredConnection, `${file}: connection must be configured by the caller`);
    assert.equal(typeof configuredConnection.value, "string");
    assert.ok(configuredConnection.value.trim(), `${file}: connection must not be empty`);
    const invocations = file.endsWith("-no-skill.yml") ? [pipeline.extends.parameters] :
      pipeline.extends.parameters.stages
        .filter((stage: { stage: string }) => ["EvalForced", "EvalTrigger"].includes(stage.stage))
        .map((stage: { jobs: { parameters: object }[] }) => stage.jobs[0].parameters);
    assert.equal(invocations.length, file.endsWith("-no-skill.yml") ? 1 : 2);
    for (const invocation of invocations) {
      assert.equal(invocation.azureSubscription, "${{ variables.TypeSpecAuthorEvalAzureSubscription }}");
      assert.equal(invocation.UseAzSdkAuthentication, "${{ eq(parameters.McpKind, 'live') }}");
      assert.equal(invocation.postEvalTemplate, undefined);
      assert.deepEqual(invocation.vallyArguments.slice(-2), ["--runs", "${{ format('{0}', parameters.Runs) }}"]);
    }
  }
});

test("all three repository configurations plan with Vally 0.19 without executing models or MCP", async (t) => {
  const requireEval = createRequire(new URL("../../../../../eng/common/scripts/eval/package.json", import.meta.url));
  const { ProjectContext, loadProjectConfig, discoverAndFilterEvals, parseSuiteFilter, planRun } =
    await import(pathToFileURL(requireEval.resolve("@microsoft/vally")).href);
  const { parse } = requireEval("yaml");
  const benchmark = parse(fs.readFileSync(new URL("../../../../../eng/pipelines/azure-typespec-author-benchmark.yml",
    import.meta.url), "utf8"));
  const baseline = parse(fs.readFileSync(new URL("../../../../../eng/pipelines/azure-typespec-author-benchmark-no-skill.yml",
    import.meta.url), "utf8"));
  const source = path.resolve(import.meta.dirname, "..");
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-benchmark-configs-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const mockDll = createMcpBinary(root, "mock");
  fs.symlinkSync(path.resolve(source, "../../azsdk-common-typespec-naming"),
    path.join(root, "azsdk-common-typespec-naming"), "junction");
  const original = await loadProjectConfig(source);
  for (const environment of Object.values(original.environments) as { skills?: string[] }[]) {
    if (environment.skills) {
      environment.skills = environment.skills.map((skill) => path.resolve(source, "evals", skill));
    }
  }
  for (const mode of ["forced", "trigger", "no-skill"]) {
    const author = path.join(root, mode);
    const evalRoot = path.join(author, "evaluate");
    fs.mkdirSync(evalRoot, { recursive: true });
    fs.copyFileSync(path.join(source, "..", "SKILL.md"), path.join(author, "SKILL.md"));
    fs.cpSync(path.join(source, "evals"), path.join(evalRoot, "evals"), { recursive: true });
    for (const directory of ["fixtures", "scripts"]) {
      fs.symlinkSync(path.join(source, directory), path.join(evalRoot, directory), "junction");
    }
    const { dir: _dir, ...config } = original;
    config.environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"].cwd = root;
    fs.writeFileSync(path.join(evalRoot, ".vally.yaml"), JSON.stringify(config));
    await prepareBenchmark(evalRoot, mode, path.join(author, "skills"), "mock");
    const context = await ProjectContext.load(path.join(evalRoot, "evals"));
    const filter = parseSuiteFilter([`mode=${mode}`]);
    const allMatching = await discoverAndFilterEvals(context, {
      suiteEvals: ["evals/*.eval.yaml"], filter,
    });
    const patterns = mode === "no-skill" ? baseline.extends.parameters.evalGlobs :
      benchmark.extends.parameters.stages.find((stage) => stage.stage ===
        (mode === "forced" ? "PrepareForced" : "PrepareTrigger")).jobs[0].parameters.evalGlobs;
    // Mirror CLI --tag + explicit -e: load every matrix-selected file before filtering.
    const discovery = await discoverAndFilterEvals(context, { suiteEvals: patterns });
    assert.deepEqual(discovery.errors, [], `${mode}: eval loading failed`);
    assert.ok(discovery.matched.length > 0, `${mode}: no selected evals`);
    assert.deepEqual(discovery.matched.map((entry) => entry.filePath).sort(),
      allMatching.matched.map((entry) => entry.filePath).sort(),
      `${mode}: pipeline globs must cover exactly the files with matching stimuli`);
    const plan = await planRun({
      variants: [{ name: "default", specs: discovery.matched }],
      projectCtx: context, models: [undefined], runs: 3, threshold: 0.8, tagFilter: filter,
      getExecutor: () => ({ name: "copilot-sdk" }),
    });
    const failures = plan.evals.filter((entry) => entry.failure);
    assert.deepEqual(failures, [], JSON.stringify(plan.diagnostics.filter((entry) => entry.severity === "error")));
    assert.ok(plan.totalItems > 0);
    assert.ok(plan.evals.every((entry) => entry.plannedStimulusCount > 0),
      `${mode}: zero-match evals would fail the standard common verdict`);
    assert.equal(plan.totalItems, allMatching.matched.reduce((sum, entry) => sum + entry.stimuli.length * 3, 0));
    const prepared = context.config.environments["azsdk-mcp"];
    assert.equal(prepared.skills.length, mode === "no-skill" ? 0 : original.environments["azsdk-mcp"].skills.length);
    assert.equal(prepared.mcpServers["azure-sdk-mcp"].command, "dotnet");
    assert.deepEqual(prepared.mcpServers["azure-sdk-mcp"].args, [mockDll]);
    assert.deepEqual(prepared.mcpServers["azure-sdk-mcp"].env, original.environments["azsdk-mcp"].mcpServers["azure-sdk-mcp"].env);
    for (const local of ["azsdk-mcp-local", "azsdk-mcp-mock-local"]) {
      assert.deepEqual(context.config.environments[local].mcpServers, original.environments[local].mcpServers);
    }
  }
});

test("the actual SkillBranch overlay removes candidate naming from an older Git tree", (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "skill-branch-overlay-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const source = path.join(root, "source");
  const checkout = path.join(root, "checkout");
  const author = path.join(source, ".github", "skills", "azure-typespec-author");
  fs.mkdirSync(path.join(author, "references"), { recursive: true });
  fs.writeFileSync(path.join(author, "SKILL.md"), "Baseline authoring skill");
  fs.writeFileSync(path.join(author, "references", "rules.md"), "Baseline rules");
  const git = (...args: string[]) => execFileSync("git", args, { cwd: source, encoding: "utf8" });
  git("init", "--quiet", "-b", "candidate");
  const commit = () => {
    git("add", ".");
    git("-c", "user.name=Skill fixture", "-c", "user.email=skill-fixture@example.invalid",
      "-c", "commit.gpgsign=false", "commit", "--quiet", "-m",
      "Skill fixture\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>");
  };
  commit();
  git("branch", "baseline");
  fs.writeFileSync(path.join(author, "SKILL.md"), "Requires azsdk-common-typespec-naming");
  const naming = path.join(source, ".github", "skills", "azsdk-common-typespec-naming");
  fs.mkdirSync(path.join(naming, "references"), { recursive: true });
  fs.writeFileSync(path.join(naming, "SKILL.md"), "Candidate naming rules");
  fs.writeFileSync(path.join(naming, "references", "rules.md"), "Candidate rules");
  commit();
  execFileSync("git", ["clone", "--quiet", source, checkout]);
  const yaml = fs.readFileSync(new URL("typespec-author-preeval.yml", import.meta.url), "utf8");
  const body = yaml.match(/  - script: \|\r?\n([\s\S]*?)\r?\n    displayName:/)?.[1];
  assert.ok(body);
  const script = body.split(/\r?\n/).map((line) => line.replace(/^ {6}/, "")).join("\n");
  const bash = process.platform === "win32" ? [
    path.resolve(execFileSync("git", ["--exec-path"], { encoding: "utf8" }).trim(), "../../../bin/bash.exe"),
    path.join(process.env.ProgramFiles ?? "C:\\Program Files", "Git", "bin", "bash.exe"),
  ].find((candidate) => fs.existsSync(candidate)) : "bash";
  assert.ok(bash, "Install Git Bash to test the pipeline overlay on Windows.");
  execFileSync(bash, ["--noprofile", "--norc", "-c", script], {
    cwd: checkout, env: { ...process.env, SKILL_BRANCH: "baseline" },
  });
  const skills = path.join(checkout, ".github", "skills");
  assert.equal(fs.readFileSync(path.join(skills, "azure-typespec-author", "SKILL.md"), "utf8"),
    "Baseline authoring skill");
  assert.equal(fs.existsSync(path.join(skills, "azsdk-common-typespec-naming", "SKILL.md")), false);
  assert.equal(fs.existsSync(path.join(skills, "azsdk-common-typespec-naming", "references")), false);
  const environment = { sdk: { skills: ["../..", "../../../azsdk-common-typespec-naming"] } };
  configureSkillEnvironments(environment, path.join(skills, "azure-typespec-author", "evaluate"),
    false, path.join(root, "runtime"));
  assert.equal(environment.sdk.skills.length, 1);
});
