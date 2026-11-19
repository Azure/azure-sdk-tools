import { assert, expect, test } from "vitest";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  ensureSkillDependencies,
  skillDependencyInstallCommand,
} from "../scripts/skill-dependencies.ts";

function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "assessment-skill-dependencies-"));
  fs.writeFileSync(
    path.join(root, "package.json"),
    JSON.stringify({ dependencies: { yaml: "^2.9.0" } }),
  );
  fs.writeFileSync(
    path.join(root, "package-lock.json"),
    JSON.stringify({
      lockfileVersion: 3,
      packages: {
        "": { dependencies: { yaml: "^2.9.0" } },
        "node_modules/yaml": { version: "2.9.0" },
      },
    }),
  );
  return root;
}

function installYaml(root: string, version: string = "2.9.0") {
  const directory = path.join(root, "node_modules", "yaml");
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(path.join(directory, "package.json"), JSON.stringify({ name: "yaml", version }));
}

void test("skips installation when locked skill dependencies are ready", async () => {
  const root = fixture();
  installYaml(root);
  const result = await ensureSkillDependencies({
    root,
    runInstall: () => assert.fail("install should not run"),
    log: () => {},
  });
  assert.equal(result.installed, false);
  assert.equal(result.packages.yaml, "2.9.0");
});

void test("installs missing or stale skill dependencies", async () => {
  for (const installed of [undefined, "2.8.0"]) {
    const root = fixture();
    if (installed) installYaml(root, installed);
    let calls = 0;
    const result = await ensureSkillDependencies({
      root,
      runInstall: (target) => {
        calls += 1;
        installYaml(target);
      },
      log: () => {},
    });
    assert.equal(calls, 1);
    assert.equal(result.installed, true);
    assert.equal(fs.existsSync(path.join(root, ".dependency-install.lock")), false);
  }
});

void test("serializes concurrent first-use installation", async () => {
  const root = fixture();

  let releaseInstall: () => void = () => {};

  let signalStarted: () => void = () => {};

  const started: Promise<void> = new Promise((resolve) => {
    signalStarted = resolve;
  });

  const released: Promise<void> = new Promise((resolve) => {
    releaseInstall = resolve;
  });
  const first = ensureSkillDependencies({
    root,
    runInstall: async (target) => {
      signalStarted();
      await released;
      installYaml(target);
    },
    log: () => {},
  });
  await started;
  const second = ensureSkillDependencies({
    root,
    runInstall: () => assert.fail("second install should not run"),
    wait: () => new Promise((resolve) => setTimeout(resolve, 1)),
    timeoutMs: 1000,
    log: () => {},
  });
  releaseInstall();
  const [firstResult, secondResult] = await Promise.all([first, second]);
  assert.equal(firstResult.installed, true);
  assert.equal(secondResult.installed, false);
});

void test("cleans the installation lock after failure", async () => {
  const root = fixture();
  await expect(
    ensureSkillDependencies({
      root,
      runInstall: () => {
        throw new Error("offline");
      },
      log: () => {},
    }),
  ).rejects.toThrow(/offline/);
  assert.equal(fs.existsSync(path.join(root, ".dependency-install.lock")), false);
});

void test("builds a deterministic lifecycle-script-free skill install", () => {
  assert.deepEqual(skillDependencyInstallCommand({ platform: "win32" }), {
    executable: "npm.cmd",
    args: ["ci", "--ignore-scripts", "--no-audit", "--no-fund"],
  });
});
