import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";

import { configureSkillEnvironments } from "./typespec-author-invoke-shard.ts";

function fixture(t: { after: (fn: () => void) => void }, naming = true, required = true) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-skill-config-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const author = path.join(root, "azure-typespec-author");
  const evalRoot = path.join(author, "evaluate");
  fs.mkdirSync(evalRoot, { recursive: true });
  for (const name of ["azure-typespec-author", ...(naming ? ["azure-typespec-sdk-naming"] : [])]) {
    const dir = path.join(root, name);
    fs.mkdirSync(path.join(dir, "references"), { recursive: true });
    fs.mkdirSync(path.join(dir, "evals"), { recursive: true });
    fs.writeFileSync(path.join(dir, "SKILL.md"), name === "azure-typespec-author" && required
      ? "Depends on azure-typespec-sdk-naming" : name);
    fs.writeFileSync(path.join(dir, "references", "rules.md"), name);
    fs.writeFileSync(path.join(dir, "evals", "secret-grader.yaml"), "must not leak");
  }
  const environments = {
    sdk: {
      skills: ["../..", "../../../azure-typespec-sdk-naming"],
      mcpServers: { sdk: { cwd: "../../.." } },
    },
  };
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
  assert.equal(fs.existsSync(path.join(stage, "azure-typespec-sdk-naming")), false);
});

test("a missing required naming dependency remains an explicit error", (t) => {
  const { evalRoot, environments, stage } = fixture(t, false, true);
  assert.throws(
    () => configureSkillEnvironments(environments, evalRoot, false, stage),
    /Missing declared benchmark skill: .*azure-typespec-sdk-naming/,
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
  fs.writeFileSync(path.join(author, "SKILL.md"), "Requires azure-typespec-sdk-naming");
  const naming = path.join(source, ".github", "skills", "azure-typespec-sdk-naming");
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
  assert.equal(fs.existsSync(path.join(skills, "azure-typespec-sdk-naming", "SKILL.md")), false);
  assert.equal(fs.existsSync(path.join(skills, "azure-typespec-sdk-naming", "references")), false);
  const environment = { sdk: { skills: ["../..", "../../../azure-typespec-sdk-naming"] } };
  configureSkillEnvironments(environment, path.join(skills, "azure-typespec-author", "evaluate"),
    false, path.join(root, "runtime"));
  assert.equal(environment.sdk.skills.length, 1);
});
