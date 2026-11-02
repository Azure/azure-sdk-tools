import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import { createRequire } from "node:module";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

type WorkflowStep = { run?: string; uses?: string; with?: Record<string, unknown> };

const repoRoot = fileURLToPath(new URL("../../..", import.meta.url));
const packageRoot = path.join(repoRoot, "eng/common/scripts/skill-eval-coverage");
const { parse } = createRequire(path.join(packageRoot, "package.json"))("yaml");

test("all shared package tests pass in an isolated consumer without Tools files", () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "skill-coverage-consumer-"));
    try {
        const consumer = path.join(root, "eng/common/scripts/skill-eval-coverage");
        fs.cpSync(packageRoot, consumer, {
            recursive: true,
            filter: (source) => path.basename(source) !== "node_modules",
        });
        fs.symlinkSync(path.join(packageRoot, "node_modules"), path.join(consumer, "node_modules"),
            process.platform === "win32" ? "junction" : "dir");
        assert.equal(fs.existsSync(path.join(root, ".github")), false);
        assert.equal(fs.existsSync(path.join(root, ".git")), false);
        assert.equal(fs.existsSync(path.join(root, "eng/common-tests")), false);
        const tests = fs.readdirSync(path.join(consumer, "test"))
            .filter((file) => file.endsWith(".test.ts"))
            .map((file) => path.join(consumer, "test", file));
        assert.ok(tests.length > 0);
        const env: NodeJS.ProcessEnv = { ...process.env, GITHUB_ACTIONS: "false", GITHUB_STEP_SUMMARY: "" };
        delete env.NODE_TEST_CONTEXT;
        const result = spawnSync(process.execPath, ["--test", "--test-reporter=spec", ...tests], {
            cwd: root, env, encoding: "utf8", timeout: 120000,
        });
        assert.ifError(result.error);
        assert.equal(result.status, 0, result.stdout + result.stderr);
    } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test("read-only workflow preserves the exact blocking lint job", () => {
    const workflow = parse(fs.readFileSync(path.join(repoRoot, ".github/workflows/skill-eval.yml"), "utf8"));
    assert.deepEqual(workflow.permissions, { contents: "read" });
    assert.equal(workflow.on.pull_request, null, "PR path filters must not suppress reverted-skill comment cleanup");
    const lint = workflow.jobs["check-skills"];
    assert.equal(lint["continue-on-error"], undefined);
    assert.equal(lint.needs, "skill-eval-coverage");
    assert.equal(lint.if, "${{ always() && needs.skill-eval-coverage.outputs.run_lint != 'false' }}");
    assert.deepEqual(lint.steps.filter((step: WorkflowStep) => step.run).map((step: WorkflowStep) => step.run), ["npm ci", "./eng/skill-eval/node_modules/.bin/vally lint ."]);
    const coverage = workflow.jobs["skill-eval-coverage"];
    assert.deepEqual(coverage.permissions, { contents: "read", "pull-requests": "read" });
    assert.equal(coverage["continue-on-error"], undefined);
    assert.equal(coverage.outputs.run_lint, "${{ steps.coverage.outputs.run_lint }}");
    assert.equal(coverage.steps.find((step: WorkflowStep) => step.run?.endsWith("github.ts")).id, "coverage");
    assert.deepEqual(Object.keys(workflow.jobs), ["skill-eval-coverage", "check-skills"]);
    assert.ok(coverage.steps.every((step: WorkflowStep) => !step.run?.includes("--test") && !step.run?.includes("npm test")));
});

test("write-enabled reporter uses only default-branch code, no PR artifacts or code checkout", () => {
    const workflow = parse(fs.readFileSync(path.join(repoRoot, ".github/workflows/skill-eval-coverage-comment.yml"), "utf8"));
    assert.deepEqual(Object.keys(workflow.on), ["workflow_run"]);
    assert.deepEqual(workflow.on.workflow_run, { workflows: ["Skill Evaluations"], types: ["completed"] });
    assert.deepEqual(workflow.permissions, {});
    const job = workflow.jobs.comment;
    assert.deepEqual(job.permissions, { contents: "read", actions: "read", "pull-requests": "write" });
    const checkout = job.steps.find((step: WorkflowStep) => step.uses?.startsWith("actions/checkout@"));
    assert.equal(checkout.with.ref, "${{ github.event.repository.default_branch }}");
    assert.equal(checkout.with["persist-credentials"], false);
    assert.equal(checkout.with["sparse-checkout-cone-mode"], false);
    assert.equal(checkout.with["sparse-checkout"], "/.github/skill-eval-coverage.json\n/eng/common/scripts/skill-eval-coverage/\n");
    assert.equal(workflow.concurrency["cancel-in-progress"], false);
    assert.ok(job.steps.some((step: WorkflowStep) => step.run?.endsWith("github.ts --post-comment")));
    assert.ok(job.steps.every((step: WorkflowStep) => !step.uses?.includes("download-artifact") && !step.run?.includes("vally eval")));
});

test("lint, coverage, script tests and trusted reporting all use Node 24", () => {
    let setupSteps = 0;
    for (const file of ["skill-eval.yml", "skill-eval-coverage-comment.yml", "skill-eval-coverage-tests.yml"]) {
        const workflow = parse(fs.readFileSync(path.join(repoRoot, ".github/workflows", file), "utf8"));
        for (const [name, job] of Object.entries(workflow.jobs) as [string, { steps: WorkflowStep[] }][]) {
            const setup = job.steps.filter((step) => step.uses?.startsWith("actions/setup-node@"));
            assert.equal(setup.length, 1, `${file}: ${name}`);
            assert.equal(String(setup[0].with?.["node-version"]), "24", `${file}: ${name}`);
            setupSteps += setup.length;
        }
    }
    assert.equal(setupSteps, 4);
});

test("coverage workflows pin actions directly to exact commits", () => {
    const files = [".github/workflows/skill-eval.yml", ".github/workflows/skill-eval-coverage-comment.yml",
        ".github/workflows/skill-eval-coverage-tests.yml"];
    assert.equal(fs.existsSync(path.join(repoRoot, ".github/workflows/actions.lock")), false);
    for (const file of files) {
        const workflow = parse(fs.readFileSync(path.join(repoRoot, file), "utf8"));
        for (const job of Object.values(workflow.jobs) as { steps: WorkflowStep[] }[]) {
            for (const step of job.steps) {
                if (step.uses) assert.match(step.uses, /^[^@\s]+@[a-f0-9]{40}$/, file);
                assert.ok(!step.run?.includes("gh actions-lock"), file);
            }
        }
    }
});

test("script tests have a separate read-only workflow and all checker installs use frozen pnpm", () => {
    const testWorkflow = parse(fs.readFileSync(path.join(repoRoot, ".github/workflows/skill-eval-coverage-tests.yml"), "utf8"));
    assert.deepEqual(testWorkflow.permissions, { contents: "read" });
    assert.deepEqual(Object.keys(testWorkflow.on).sort(), ["pull_request", "push", "workflow_dispatch"]);
    for (const event of ["pull_request", "push"]) {
        assert.ok(testWorkflow.on[event].paths.includes("eng/common-tests/skill-eval-coverage/**"));
    }
    assert.ok(testWorkflow.jobs.test.steps.some((step: WorkflowStep) => step.run === "node --test eng/common/scripts/skill-eval-coverage/test/*.test.ts eng/common-tests/skill-eval-coverage/*.test.ts"));
    assert.equal(testWorkflow.jobs.test["continue-on-error"], undefined);
    for (const [file, name] of [["skill-eval.yml", "skill-eval-coverage"], ["skill-eval-coverage-comment.yml", "comment"],
        ["skill-eval-coverage-tests.yml", "test"]]) {
        const workflow = parse(fs.readFileSync(path.join(repoRoot, ".github/workflows", file), "utf8"));
        const install = workflow.jobs[name].steps.find((step: WorkflowStep) => step.run?.includes("pnpm.mjs"));
        assert.ok(install, file);
        assert.match(install.run, /pnpm@12\.6\.0/);
        assert.match(install.run, /--ignore-workspace install --pm-on-fail=ignore --frozen-lockfile --prod --ignore-scripts/);
        assert.equal(install.env, undefined, "checker dependency installs must not receive the GitHub token");
    }
});