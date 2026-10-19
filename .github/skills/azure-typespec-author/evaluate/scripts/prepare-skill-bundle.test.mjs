import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { prepareSkillBundle } from "./prepare-skill-bundle.mjs";

const repoRoot = fileURLToPath(new URL("../../../../../", import.meta.url));

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "naming-skill-bundle-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  for (const skill of [
    "azure-typespec-author",
    "azsdk-common-typespec-naming",
    "azure-typespec-assessment",
    "azsdk-common-generate-sdk-locally",
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

test("stages benchmark skills and routing competitors without eval content", (t) => {
  const root = fixture(t);
  const output = path.join(root, "bundle");
  prepareSkillBundle(root, output);
  assert.deepEqual(fs.readdirSync(output).sort(), [
    "azsdk-common-generate-sdk-locally",
    "azsdk-common-typespec-naming",
    "azure-typespec-assessment",
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

test("numbered naming cases compare task quality without mounting baseline skills", () => {
  const evalDirectory = path.join(
    repoRoot,
    ".github",
    "skills",
    "azure-typespec-author",
    "evaluate",
    "evals",
  );
  const files = fs
    .readdirSync(evalDirectory)
    .filter((file) => /^007\d{3}\.eval\.yaml$/.test(file));
  assert.equal(files.length, 14);
  for (const file of files) {
    const content = fs.readFileSync(path.join(evalDirectory, file), "utf8");
    assert.match(content, /^environment: azsdk-mcp$/m);
    assert.doesNotMatch(content, /^\s*skills:/m);
    assert.match(content, /^\s+mode: trigger$/m);
    if (Number(file.slice(3, 6)) > 8) continue;
    assert.match(content, /^\s+mode: forced$/m);
    assert.match(content, /^\s+mode: no-skill$/m);
    const baseline = content.slice(content.indexOf("    mode: no-skill"));
    assert.doesNotMatch(baseline, /type: skill-invocation/);
    assert.doesNotMatch(baseline, /pattern: .*naming.*\\\.md/);
    assert.match(baseline, /graders:\s*\n/);
  }
});

test("shared eval discovery includes naming without overriding baseline skill selection", () => {
  const config = fs.readFileSync(
    path.join(
      repoRoot,
      ".github",
      "skills",
      "azure-typespec-author",
      "evaluate",
      ".vally.yaml",
    ),
    "utf8",
  );
  assert.doesNotMatch(config, /^\s*skills:/m);
  assert.doesNotMatch(config, /evals\/naming\//);
  assert.match(
    config,
    /naming-no-skill:\s+evals: \["evals\/\*\.eval\.yaml"\]\s+filter: \{ suite: naming, mode: no-skill \}/,
  );
  const pipeline = fs.readFileSync(
    path.join(repoRoot, "eng", "pipelines", "azure-typespec-author-benchmark.yml"),
    "utf8",
  );
  assert.doesNotMatch(pipeline, /evals\/naming\//);
  assert.match(
    pipeline,
    /--skill-dir \$\(Build\.SourcesDirectory\)\/artifacts\/typespec-author-skills/,
  );
});

test("SkillBranch overlays authoring and naming before bundle preparation", () => {
  const preeval = fs.readFileSync(
    path.join(
      repoRoot,
      ".github",
      "skills",
      "azure-typespec-author",
      "evaluate",
      "pipeline",
      "typespec-author-preeval.yml",
    ),
    "utf8",
  );
  assert.equal(
    preeval.match(/\.github\/skills\/azsdk-common-typespec-naming\/SKILL\.md/g)
      ?.length,
    2,
  );
  assert.equal(
    preeval.match(/\.github\/skills\/azsdk-common-typespec-naming\/references/g)
      ?.length,
    2,
  );
  assert.ok(
    preeval.indexOf("azsdk-common-typespec-naming/SKILL.md") <
      preeval.indexOf("prepare-skill-bundle.mjs"),
  );
});
