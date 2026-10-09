import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { prepareSkillBundle } from "./prepare-skill-bundle.mjs";

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "naming-skill-bundle-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  for (const skill of [
    "azure-typespec-author",
    "azsdk-common-typespec-naming",
  ]) {
    const source = path.join(root, ".github", "skills", skill);
    fs.mkdirSync(path.join(source, "references"), { recursive: true });
    fs.mkdirSync(path.join(source, "evals"));
    fs.writeFileSync(path.join(source, "SKILL.md"), skill);
    fs.writeFileSync(
      path.join(source, "references", "rules.md"),
      "shared rules",
    );
    fs.writeFileSync(path.join(source, "evals", "eval.yaml"), "not staged");
  }
  return root;
}

test("stages both skill definitions and references, excluding evals", (t) => {
  const root = fixture(t);
  const output = path.join(root, "bundle");
  prepareSkillBundle(root, output);
  assert.deepEqual(fs.readdirSync(output).sort(), [
    "azsdk-common-typespec-naming",
    "azure-typespec-author",
  ]);
  for (const skill of fs.readdirSync(output)) {
    assert.equal(
      fs.readFileSync(path.join(output, skill, "SKILL.md"), "utf8"),
      skill,
    );
    assert.equal(
      fs.existsSync(path.join(output, skill, "references", "rules.md")),
      true,
    );
    assert.equal(fs.existsSync(path.join(output, skill, "evals")), false);
  }
});

test("reruns remove stale references but preserve unrelated bundle contents", (t) => {
  const root = fixture(t);
  const output = path.join(root, "bundle");
  prepareSkillBundle(root, output);
  fs.writeFileSync(path.join(output, "sentinel"), "keep");
  fs.writeFileSync(
    path.join(output, "azure-typespec-author", "references", "stale.md"),
    "",
  );
  prepareSkillBundle(root, output);
  assert.equal(
    fs.existsSync(
      path.join(output, "azure-typespec-author", "references", "stale.md"),
    ),
    false,
  );
  assert.equal(fs.readFileSync(path.join(output, "sentinel"), "utf8"), "keep");
});

test("missing naming dependency fails before changing the existing bundle", (t) => {
  const root = fixture(t);
  const output = path.join(root, "bundle");
  prepareSkillBundle(root, output);
  fs.unlinkSync(
    path.join(
      root,
      ".github",
      "skills",
      "azsdk-common-typespec-naming",
      "SKILL.md",
    ),
  );
  assert.throws(
    () => prepareSkillBundle(root, output),
    /Missing benchmark skill input/,
  );
  assert.equal(
    fs.existsSync(path.join(output, "azure-typespec-author", "SKILL.md")),
    true,
  );
});
