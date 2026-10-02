import assert from "node:assert/strict";
import { test } from "node:test";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { mkdtemp, readdir, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { parseDocument } from "yaml";

const root = resolve(import.meta.dirname, "..");
test("pipeline templates parse and deployment is opt-in with unset resource values", async () => {
  for (const name of ["ci.yml", "pipelines/deploy-stage.yml", "pipelines/publish-results.yml"]) {
    const document = parseDocument(await readFile(join(root, name), "utf8"), { uniqueKeys: true });
    assert.deepEqual(document.errors, [], name);
    const parsed = document.toJS();
    if (name === "ci.yml") {
      assert.equal(parsed.parameters.find((value) => value.name === "deployDashboard").default, false);
      for (const parameter of parsed.parameters.filter((value) => value.name !== "deployDashboard")) assert.equal(parameter.default, "");
      assert.ok(Object.keys(parsed.extends.parameters.stages.at(-1))[0].includes("parameters.deployDashboard"));
    }
    if (name === "pipelines/deploy-stage.yml") {
      const steps = parsed.stages[0].jobs[0].strategy.runOnce.deploy.steps;
      assert.ok(steps.findIndex((step) => step.task === "AzureCLI@2") < steps.findIndex((step) => step.task === "AzureRmWebAppDeployment@4"));
    }
  }
});

test("deployment staging includes only runtime source, not local data, credentials, or tests", async (t) => {
  const temporary = await mkdtemp(join(tmpdir(), "deploy-stage-test-"));
  t.after(() => rm(temporary, { recursive: true, force: true }));
  const output = join(temporary, "app");
  const env = { ...process.env }; delete env.NODE_TEST_CONTEXT;
  await promisify(execFile)(process.execPath, [join(root, "scripts/stage-deployment.mjs"), output], { env });
  assert.deepEqual((await readdir(output)).sort(), ["lib", "package-lock.json", "package.json", "pipelines.js", "scripts", "server.js", "start.js"]);
  const scripts = await readdir(join(output, "scripts"));
  assert.ok(scripts.includes("check-deployment-target.mjs"));
  assert.ok(!scripts.includes("seed-poc.mjs"));
  await assert.rejects(promisify(execFile)(process.execPath, [join(root, "scripts/stage-deployment.mjs"), output], { env }), /must be empty/);
});