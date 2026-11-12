import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test, type TestContext } from "node:test";
import { fixtureDependencies } from "./setup-fixture-files.ts";

const rulesets = "@azure-tools/typespec-azure-rulesets";
const originalRulesets = `${rulesets}-original`;
const aliasVersion = `npm:${rulesets}@0.73.0`;

function hasStringDevDependencies(
  value: unknown,
): value is { devDependencies: Record<string, string> } {
  if (
    typeof value !== "object" ||
    value === null ||
    !("devDependencies" in value) ||
    typeof value.devDependencies !== "object" ||
    value.devDependencies === null
  ) {
    return false;
  }
  return Object.values(value.devDependencies).every((version) => typeof version === "string");
}

function fixture(t: TestContext, workspaceRulesets = false) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "setup-fixture-files-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const specRoot = path.join(root, "spec repo");
  fs.mkdirSync(path.join(specRoot, ".github"), { recursive: true });
  const evaluate = path.join(root, ".github", "skills", "azure-typespec-author", "evaluate");
  const scripts = path.join(evaluate, "scripts");
  fs.mkdirSync(scripts, { recursive: true });
  fs.copyFileSync(
    new URL("setup-fixture-files.ts", import.meta.url),
    path.join(scripts, "setup.ts"),
  );
  fs.writeFileSync(path.join(specRoot, ".github", "copilot-instructions.md"), "# Test instructions\n");
  const devDependencies = Object.fromEntries(fixtureDependencies.map((name) => [name, "catalog:"]));
  const catalog = Object.fromEntries(fixtureDependencies.map((name) => [name, "0.73.0"]));
  if (workspaceRulesets) {
    devDependencies[rulesets] = "workspace:*";
    devDependencies[originalRulesets] = "catalog:";
    delete catalog[rulesets];
    catalog[originalRulesets] = aliasVersion;
  }
  fs.writeFileSync(
    path.join(specRoot, "package.json"),
    JSON.stringify({ type: "module", devDependencies }),
  );
  function writeCatalog() {
    fs.writeFileSync(
      path.join(specRoot, "pnpm-workspace.yaml"),
      `packages:\n  - "eng/*"\ncatalog:\n${Object.entries(catalog)
        .map(([name, version]) => `  "${name}": ${version}`)
        .join("\n")}\n`,
    );
  }
  writeCatalog();
  return {
    root,
    specRoot,
    catalog,
    writeCatalog,
    packageFile: path.join(evaluate, "fixtures", "Microsoft.Widget", "Widget", "package.json"),
    instructionsFile: path.join(
      evaluate,
      "fixtures",
      "instructions-test",
      "copilot-instructions.md",
    ),
    run: () => spawnSync(process.execPath, ["--experimental-strip-types", path.join(scripts, "setup.ts"),
      "--spec-root", specRoot], { encoding: "utf8" }),
  };
}

await test("generates catalog dependencies and copies repository instructions", (t) => {
  const setup = fixture(t);
  const result = setup.run();
  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.deepEqual(JSON.parse(fs.readFileSync(setup.packageFile, "utf8")), {
    name: "microsoft-widget-eval-fixture",
    private: true,
    type: "module",
    devDependencies: setup.catalog,
  });
  assert.equal(fs.readFileSync(setup.instructionsFile, "utf8"), "# Test instructions\n");
});

await test("replaces the workspace ruleset with its installable upstream npm alias", (t) => {
  const setup = fixture(t, true);
  const result = setup.run();
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const generated: unknown = JSON.parse(fs.readFileSync(setup.packageFile, "utf8"));
  assert.ok(hasStringDevDependencies(generated));
  const { [originalRulesets]: _alias, ...catalog } = setup.catalog;
  assert.deepEqual(generated.devDependencies, { ...catalog, [rulesets]: aliasVersion });
  assert.equal(fs.readFileSync(setup.instructionsFile, "utf8"), "# Test instructions\n");
});

await test("fails before writing fixtures when a required catalog dependency is missing", (t) => {
  const setup = fixture(t);
  delete setup.catalog["@typespec/compiler"];
  setup.writeCatalog();
  const result = setup.run();
  assert.notEqual(result.status, 0);
  assert.match(
    result.stderr,
    /Required dependency is missing from the pnpm catalog or is not installable: @typespec\/compiler/,
  );
  assert.equal(fs.existsSync(setup.packageFile), false);
});

await test("requires the upstream alias for the workspace ruleset", (t) => {
  const setup = fixture(t, true);
  delete setup.catalog[originalRulesets];
  setup.writeCatalog();
  const result = setup.run();
  assert.notEqual(result.status, 0);
  assert.match(
    result.stderr,
    /Required dependency is missing from the pnpm catalog or is not installable: @azure-tools\/typespec-azure-rulesets/,
  );
  assert.equal(fs.existsSync(setup.packageFile), false);
});

await test("reports missing repository inputs before writing fixtures", (t) => {
  const setup = fixture(t);
  fs.rmSync(path.join(setup.specRoot, "pnpm-workspace.yaml"));
  const result = setup.run();
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Required fixture input does not exist: .*pnpm-workspace\.yaml/);
  assert.equal(fs.existsSync(setup.packageFile), false);
});

await test("rejects workspace dependencies before writing a standalone fixture", (t) => {
  const setup = fixture(t);
  setup.catalog["@typespec/compiler"] = "workspace:*";
  setup.writeCatalog();
  const result = setup.run();
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /not installable: @typespec\/compiler/);
  assert.equal(fs.existsSync(setup.packageFile), false);
});
