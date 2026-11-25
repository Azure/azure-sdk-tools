import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

const evaluate = fileURLToPath(new URL("../", import.meta.url));
const modules = path.resolve(process.env.FIXTURE_NODE_MODULES ??
  path.join(evaluate, "fixtures", "Microsoft.Widget", "Widget", "node_modules"));
assert.ok(fs.existsSync(modules), "Prepare FIXTURE_NODE_MODULES before running compiler-backed tests.");
const require = createRequire(fs.realpathSync(path.join(modules, "@typespec", "compiler", "package.json")));
const { parseDocument } = require("yaml");
const sourceCases = [
  "104026", "104039", "104044", "105056", "105057", "105058", "105059", "104029",
  "104045", "104008", "105071", "104023", "105079", "105080", "105083", "105086", "105098",
];
const fixtures = fs.readdirSync(path.join(evaluate, "fixtures")).flatMap((name) => {
  const directory = path.join(evaluate, "fixtures", name);
  const file = path.join(directory, "contract.json");
  return fs.existsSync(file) ? [{ directory, ...JSON.parse(fs.readFileSync(file, "utf8")) }] : [];
});
const bySource = (id) => fixtures.find((fixture) => fixture.sourceCase === id);

function workspace(t, fixture, state) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), `discussion-${fixture.id}-`));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.symlinkSync(modules, path.join(root, "node_modules"), "junction");
  fs.writeFileSync(path.join(root, "package.json"), '{"type":"module"}\n');
  for (const name of ["main.tsp", "employee.tsp", "shared.tsp", "readme.md", "tspconfig.yaml"]) {
    const source = name === "employee.tsp"
      ? path.join(evaluate, "fixtures", "discussion-widget", name)
      : path.join(evaluate, "fixtures", "Microsoft.Widget", "Widget", name);
    fs.copyFileSync(source, path.join(root, name));
  }
  for (const name of fs.readdirSync(path.join(fixture.directory, state))) {
    fs.copyFileSync(path.join(fixture.directory, state, name), path.join(root, name));
  }
  fs.copyFileSync(path.join(fixture.directory, "contract.json"), path.join(root, "discussion-contract.json"));
  fs.copyFileSync(path.join(evaluate, "scripts", "check-discussion-case.mjs"), path.join(root, "check.mjs"));
  return root;
}

function run(root, args) {
  const result = spawnSync(process.execPath, args, { cwd: root, encoding: "utf8", timeout: 120000 });
  assert.ifError(result.error);
  return result;
}

function accepts(result) {
  assert.equal(result.status, 0, result.stdout + result.stderr);
}

function rejects(result) {
  assert.notEqual(result.status, 0, "The incorrect source unexpectedly passed the compiled contract.");
  assert.doesNotMatch(result.stderr, /ERR_MODULE_NOT_FOUND|Cannot find module/,
    "A missing dependency is not a valid negative result.");
}

test("the conversion contains exactly the 17 selected chapters and the requested category allocation", () => {
  assert.deepEqual(fixtures.map((fixture) => fixture.sourceCase).sort(), [...sourceCases].sort());
  assert.equal(new Set(fixtures.map((fixture) => fixture.id)).size, 17);
  assert.deepEqual(["001", "002", "003", "004"].map(
    (prefix) => fixtures.filter((fixture) => fixture.id.startsWith(prefix)).length,
  ), [6, 4, 0, 7]);
  assert.equal(fixtures.filter((fixture) => fixture.mode === "edit").length, 16);
  assert.equal(fixtures.filter((fixture) => fixture.mode === "diagnosis").length, 1);
});

test("all YAML modes have valid fixture mappings and share the same outcome graders", () => {
  const names = new Set();
  for (const fixture of fixtures) {
    const location = path.join(evaluate, "evals", fixture.id + ".eval.yaml");
    const document = parseDocument(fs.readFileSync(location, "utf8"), { uniqueKeys: true });
    assert.deepEqual(document.errors, [], location);
    const specification = document.toJS();
    assert.equal(specification.environment, "azsdk-mcp");
    assert.deepEqual(specification.stimuli.map((stimulus) => stimulus.tags.mode),
      ["trigger", "forced", "no-skill"]);
    assert.equal(specification.area, fixture.id.startsWith("001") ? "versioning" :
      fixture.id.startsWith("002") ? "armtemplate" : "decorators");
    for (const stimulus of specification.stimuli) {
      assert.equal(names.has(stimulus.name), false, `Duplicate stimulus ${stimulus.name}`);
      names.add(stimulus.name);
      assert.equal(stimulus.tags.suite, specification.area);
      assert.equal(stimulus.prompt.startsWith("@azure-typespec-author "), stimulus.tags.mode === "forced");
      const destinations = new Set();
      for (const file of stimulus.environment.files) {
        assert.equal(destinations.has(file.dest), false, `Duplicate fixture destination ${file.dest}`);
        destinations.add(file.dest);
        assert.equal(file.src.includes("/expected/"), false, "Never mount reference targets in a trial.");
        const source = path.resolve(path.dirname(location), ...file.src.split("/"));
        if (file.src !== "../fixtures/Microsoft.Widget/Widget/package.json") {
          assert.ok(fs.existsSync(source), `Missing fixture ${source}`);
        }
      }
      const graders = stimulus.graders.filter((grader) => grader.type !== "skill-invocation");
      assert.deepEqual(graders, specification.stimuli[2].graders,
        "Trigger, forced, and no-skill modes must grade the same actual outcome.");
      assert.ok(graders.some((grader) => grader.config.command === "node check-discussion-case.mjs"));
      assert.equal(graders.some((grader) => grader.config.command === "tsp compile ."), fixture.mode === "edit");
    }
  }
  assert.equal(names.size, 51);
});

test("the bootstrap resolves compiler-private YAML without relying on a hoisted dependency", (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "discussion-module-resolution-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const isolatedModules = path.join(root, "node_modules");
  fs.mkdirSync(path.join(isolatedModules, "@typespec"), { recursive: true });
  fs.symlinkSync(fs.realpathSync(path.join(modules, "@typespec", "compiler")),
    path.join(isolatedModules, "@typespec", "compiler"), "junction");
  assert.equal(fs.existsSync(path.join(isolatedModules, "yaml")), false);
  const environment = { ...process.env, FIXTURE_NODE_MODULES: isolatedModules };
  delete environment.NODE_TEST_CONTEXT;
  const result = spawnSync(process.execPath, [
    "--test", "--test-reporter=spec", "--test-name-pattern=conversion contains|YAML modes|constructor output",
    fileURLToPath(import.meta.url),
  ], {
    encoding: "utf8", timeout: 30000,
    env: environment,
  });
  assert.ifError(result.error);
  accepts(result);
  assert.match(result.stdout, /all YAML modes have valid fixture mappings/);
  assert.match(result.stdout, /tests 3\b/);
});

for (const fixture of fixtures) {
  test(`${fixture.id}: the input reproduces its declared state`, (t) => {
    const root = workspace(t, fixture, "input");
    accepts(run(root, ["check.mjs", "input"]));
  });
  if (fixture.mode === "edit") {
    test(`${fixture.id}: the independent reference target compiles and satisfies the contract`, (t) => {
      const root = workspace(t, fixture, "expected");
      accepts(run(root, [path.join(modules, "@typespec", "compiler", "cmd", "tsp.js"), "compile", "."]));
      accepts(run(root, ["check.mjs"]));
    });
    test(`${fixture.id}: the unchanged input cannot pass as a completed edit`, (t) => {
      const root = workspace(t, fixture, "input");
      rejects(run(root, ["check.mjs"]));
    });
  } else {
    test(`${fixture.id}: the no-edit diagnosis keeps the input intact without inventing a target`, (t) => {
      assert.equal(fs.existsSync(path.join(fixture.directory, "expected")), false);
      const root = workspace(t, fixture, "input");
      accepts(run(root, ["check.mjs"]));
    });
  }
}

test("client access written only in comments cannot satisfy versioned metadata checks", (t) => {
  const fixture = bySource("105086");
  const root = workspace(t, fixture, "expected");
  const original = fs.readFileSync(path.join(fixture.directory, "input", "client.tsp"), "utf8");
  const target = fs.readFileSync(path.join(fixture.directory, "expected", "client.tsp"), "utf8");
  fs.writeFileSync(path.join(root, "client.tsp"), original + "\n/*\n" + target + "\n*/\n");
  rejects(run(root, ["check.mjs"]));
});

test("the historical client model must stay internal, not just exist in the correct version", (t) => {
  const fixture = bySource("105086");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "client.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("@access(Access.internal)", "@access(Access.public)"));
  rejects(run(root, ["check.mjs"]));
});

test("the historical client model must retain its public schema name", (t) => {
  const fixture = bySource("105086");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "client.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace(/@renamedFrom\([^\n]+\)\r?\n/, ""));
  rejects(run(root, ["check.mjs"]));
});

test("inferred multipart output and a commented header do not satisfy the explicit-header request", (t) => {
  const fixture = bySource("105058");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "employee.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8")
    .replace('@header contentType: "multipart/form-data",', '// @header contentType: "multipart/form-data",'));
  rejects(run(root, ["check.mjs"]));
});

test("an extensible discriminator still fails when the unsupported suppression is retained", (t) => {
  const fixture = bySource("105080");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "employee.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace('@discriminator("kind")',
    '#suppress "@azure-tools/typespec-azure-core/no-string-discriminator" "Go requires a plain string."\n@discriminator("kind")'));
  rejects(run(root, ["check.mjs"]));
});

test("the nonversioned property rename must also reach the stable projection", (t) => {
  const fixture = bySource("105056");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "employee.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace(
    "  priorityValue8021ActionCluster?: string;",
    '  @TypeSpec.Versioning.added(Versions.v2024_10_01_preview)\n  priorityValue8021ActionCluster?: string;\n' +
    '  @TypeSpec.Versioning.removed(Versions.v2024_10_01_preview)\n  priorityValue8021Action_Cluster?: string;',
  ));
  rejects(run(root, ["check.mjs"]));
});

test("the b discriminator leaf must not gain the a variant's dependent field", (t) => {
  const fixture = bySource("104029");
  const root = workspace(t, fixture, "expected");
  const file = path.join(root, "employee.tsp");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("  fooB: FooB;", "  fooB: FooB;\n  fooA: FooA;"));
  rejects(run(root, ["check.mjs"]));
});

test("a private preview candidate must contain real versioned create source, not a placeholder", (t) => {
  const fixture = bySource("105083");
  const root = workspace(t, fixture, "expected");
  fs.writeFileSync(path.join(root, "private-preview.tsp"), "// v2024_10_01_preview: 2024-10-01-preview\n");
  rejects(run(root, ["check.mjs"]));
});

test("valid but stale Swagger cannot hide an unfixed source property", (t) => {
  const fixture = bySource("105056");
  const root = workspace(t, fixture, "expected");
  accepts(run(root, [path.join(modules, "@typespec", "compiler", "cmd", "tsp.js"), "compile", "."]));
  fs.copyFileSync(path.join(fixture.directory, "input", "employee.tsp"), path.join(root, "employee.tsp"));
  rejects(run(root, ["check.mjs"]));
});

test("constructor diagnosis rejects a model-access edit even when the changed source compiles", (t) => {
  const fixture = bySource("104023");
  const root = workspace(t, fixture, "input");
  fs.appendFileSync(path.join(root, "client.tsp"),
    "\n@@access(Microsoft.Widget.Employee, Access.public);\n");
  rejects(run(root, ["check.mjs"]));
});

test("constructor output assertions require every evidence-boundary boolean", () => {
  const specification = parseDocument(fs.readFileSync(path.join(evaluate, "evals", "004007.eval.yaml"), "utf8")).toJS();
  const pattern = new RegExp(specification.stimuli[0].graders
    .find((grader) => grader.name === "constructor-evidence-boundary").config.pattern);
  const expected = {
    constructorVisibilityVerified: false, typeSpecChangeApproved: false,
    sdkLanguageRequired: true, generatorEvidenceRequired: true,
    explanation: "Model access does not establish constructor visibility.",
    missingEvidence: ["SDK language", "language emitter and version", "generated constructor reproduction"],
  };
  assert.match(JSON.stringify(expected), pattern);
  for (const key of ["constructorVisibilityVerified", "typeSpecChangeApproved", "sdkLanguageRequired", "generatorEvidenceRequired"]) {
    assert.doesNotMatch(JSON.stringify({ ...expected, [key]: !expected[key] }), pattern, key);
  }
});
