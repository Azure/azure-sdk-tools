import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtemp, mkdir, writeFile, readFile, rm, access } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { collectCheckout } from "../collect.ts";
import { parseSnapshot } from "../dashboard/data.ts";

test("native local entry point uses actual tracked identity, excludes untracked source and reports modifications honestly", async () => {
  const directory = await mkdtemp(join(tmpdir(), "metrics-checkout-"));
  const root = join(directory, "repo");
  const output = join(directory, "output");
  try {
    await mkdir(join(root, "sdk", "demo", "azure-demo", "azure_demo"), { recursive: true });
    await writeFile(join(root, "sdk", "demo", "azure-demo", "pyproject.toml"),
      '[project]\nname="azure-demo"\n[tool.setuptools.packages.find]\ninclude=["azure_demo*"]\n');
    const code = join(root, "sdk", "demo", "azure-demo", "azure_demo", "client.py");
    await writeFile(code, "source = 1\n");
    const git = (...args: string[]) => {
      const result = spawnSync("git", ["-C", root, ...args], { encoding: "utf8" });
      if (result.error) throw result.error;
      assert.equal(result.status, 0, result.stderr);
      return result.stdout.trim();
    };
    git("init", "--quiet");
    git("config", "user.name", "Metrics test");
    git("config", "user.email", "metrics-test@example.invalid");
    git("remote", "add", "origin", "https://github.com/Azure/azure-sdk-for-python.git");
    git("add", ".");
    git("commit", "--quiet", "-m", "Committed source fixture\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>\nCopilot-Session: 9eb7ed2d-3e56-48c5-9c3c-7bad35e81fae");
    const commit = git("rev-parse", "HEAD");
    await writeFile(join(root, "sdk", "demo", "azure-demo", "azure_demo", "untracked.py"), "not committed\nnot counted\n");
    const first = parseSnapshot(await readFile(await collectCheckout("python", root, output), "utf8"));
    assert.equal(first.repository.commit, commit);
    assert.equal(first.repository.isDirty, false);
    assert.equal(first.summary.totalLines, 1);
    assert.equal(first.summary.totalFiles, 1);
    await writeFile(code, "source = 1\nmodified = 2\n");
    const second = parseSnapshot(await readFile(await collectCheckout("python", root, output), "utf8"));
    assert.equal(second.repository.commit, commit);
    assert.equal(second.repository.isDirty, true);
    assert.equal(second.summary.totalLines, 2);
    assert.equal(git("rev-parse", "HEAD"), commit);
    git("remote", "set-url", "origin", "https://github.com/Azure/azure-sdk-for-net.git");
    const refused = join(directory, "refused");
    await assert.rejects(collectCheckout("python", root, refused), /actual Azure\/azure-sdk-for-python tracked checkout/);
    await assert.rejects(access(refused), { code: "ENOENT" });
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
