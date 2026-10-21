// Offline regressions: load real YAML and materialize only its local files/commands.
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { basename, dirname, resolve } from "node:path";
import { test } from "node:test";
import { discoverAndLoadEvals, materializeWorkspace, ProjectContext } from "@microsoft/vally";

const repoRoot = resolve(import.meta.dirname, "../../..");
const loaded = (await Promise.all(["evals", ".github/skills"].map(async (directory) => {
    const project = await ProjectContext.load(resolve(repoRoot, directory));
    const result = await discoverAndLoadEvals(project, { suiteParams: project.config.params });
    assert.deepEqual(result.errors, []);
    return result.evals;
}))).flat();
function suite(path: string) {
    const found = loaded.find(({ filePath }) => resolve(filePath) === resolve(repoRoot, path));
    assert.ok(found, `Missing eval: ${path}`);
    return found;
}
const multi = suite("evals/workflows/mock/multi-turn-release-workflows.eval.yaml");
const planner = suite("evals/workflows/mock/release-planner-workflows.eval.yaml");
const schedule = suite(".github/skills/azsdk-common-prepare-release-plan/evals/schedule-risk.eval.yaml");
const tools = suite("evals/tools/prompt-to-tool-releaseplan.eval.yaml");
const packagePath = "azure-sdk-for-python/sdk/contosowidgetmanager/azure-contoso-widgetmanager";

for (const [entry, anchor, localNames] of [
    [multi, "create-then-generate-conversation", ["vague-then-local-conversation", "local-then-switch-pipeline-conversation"]],
    [planner, "run-sdk-generation-all-languages-for-release-pipeline-skill", ["local-single-language-routes-to-local"]],
] as const) {
    test(`${basename(entry.filePath)} materializes its declared nested Python Git fixture`, async (t) => {
        const stimulus = entry.spec.stimuli.find(({ name }) => name === anchor);
        assert.ok(stimulus);
        const env = stimulus.environment;
        assert.ok(env && typeof env === "object");
        assert.deepEqual(env.files, [{ src: "../../fixtures/sdk-repos/azure-sdk-for-python", dest: "azure-sdk-for-python" }]);
        assert.equal(env.commands?.length, 1);
        // Fail closed before executing YAML: allow only the four local fixture operations.
        const operations = env.commands[0].split(" && ");
        assert.equal(operations.length, 4);
        for (const command of operations) {
            assert.match(command, /^git -C azure-sdk-for-python (?:init -q|remote add origin https:\/\/github\.com\/Azure\/azure-sdk-for-python\.git|add \.|-c user\.email=vally@local -c user\.name=vally commit -qm "seed Python SDK fixture")$/);
        }
        // Deliberately exclude environment.git (specs cache) and skills: no clones or agents.
        const workspace = await materializeWorkspace({ files: env.files, commands: env.commands }, dirname(entry.filePath));
        try {
            const root = resolve(workspace.workDir, "azure-sdk-for-python");
            const pkg = resolve(workspace.workDir, packagePath);
            const git = (...args: string[]) => execFileSync("git", ["-C", pkg, ...args], { encoding: "utf8", timeout: 10000 }).trim();
            assert.ok(existsSync(resolve(root, ".git")));
            assert.equal(resolve(git("rev-parse", "--show-toplevel")), root);
            assert.equal(git("remote", "get-url", "origin"), "https://github.com/Azure/azure-sdk-for-python.git");
            assert.equal(git("status", "--porcelain"), "");
            const metadata = readFileSync(resolve(pkg, "pyproject.toml"), "utf8");
            assert.match(metadata, /^name\s*=\s*"azure-contoso-widgetmanager"$/m);
            assert.match(metadata, /^version\s*=\s*"1\.0\.0b1"$/m);
            for (const name of localNames) {
                const local = entry.spec.stimuli.find((item) => item.name === name);
                assert.ok(local, name);
                assert.deepEqual(local.environment, env, `${name} must share the fixture anchor`);
                assert.ok(local.prompt?.includes(packagePath), `${name} must name the nested package`);
            }
            t.diagnostic(`Native Vally materialization: nested repo, exact origin, clean package and metadata verified; ${localNames.length} local prompt paths`);
        } finally {
            await workspace.cleanup();
        }
        assert.equal(existsSync(workspace.workDir), false, "Native cleanup must remove its temp workspace");
    });
}

test("trigger graders mount every required skill and the anti-trigger requires its competitor", () => {
    const localSkill = "azsdk-common-generate-sdk-locally";
    const competitor = "azsdk-common-sdk-breaking-change";
    for (const skill of [localSkill, competitor]) {
        const entry = suite(`.github/skills/${skill}/evals/trigger.eval.yaml`);
        assert.equal(entry.spec.defaults?.model, "gpt-5.6-sol");
        for (const stimulus of entry.spec.stimuli) {
            const env = stimulus.environment;
            assert.ok(env && typeof env === "object");
            const mounted = (env.skills ?? []).map((path) => basename(resolve(dirname(entry.filePath), path)));
            for (const grader of stimulus.graders ?? []) {
                if (grader.type !== "skill-invocation") continue;
                const required = grader.config?.required ?? [];
                assert.ok(Array.isArray(required));
                for (const name of required) assert.ok(mounted.includes(name), `${stimulus.name}: missing ${name}`);
            }
            if (stimulus.name === "anti-trigger-mitigate-breaking-change") {
                assert.ok(mounted.includes(localSkill) && mounted.includes(competitor));
                const grader = stimulus.graders?.find(({ type }) => type === "skill-invocation");
                assert.deepEqual(grader?.config?.required, [competitor]);
                assert.deepEqual(grader?.config?.disallowed, [localSkill]);
            }
        }
    }
});

test("all five creation prompts use next calendar month without rewriting API versions", () => {
    const cases = [
        [multi, "create-then-generate-conversation"],
        [planner, "create-public-preview-release-plan"],
        [planner, "create-release-plan-and-generate-sdk"],
        [schedule, "create-relays-past-due-guidance"],
        [tools, "create-release-plan"],
    ] as const;
    for (const [entry, name] of cases) {
        const stimulus = entry.spec.stimuli.find((item) => item.name === name);
        assert.ok(stimulus, name);
        const prompt = stimulus.prompt ?? "";
        assert.match(prompt, /next calendar month/);
        assert.match(prompt, /explicit month and year\s+using the current date/);
        assert.doesNotMatch(prompt, /June 2026|December 2025/);
        if (entry !== schedule) assert.match(prompt, /2022-11-01-preview/);
        assert.ok(stimulus.graders?.some(({ type }) => type === "tool-calls"));
    }
    assert.ok(schedule.spec.stimuli.every(({ graders }) => graders?.some(({ type }) => type === "output-matches")));
});

test("workflow prompts use canonical mock work items and retain private-preview generation guards", () => {
    let explicitIds = 0;
    for (const entry of [multi, planner]) {
        for (const stimulus of entry.spec.stimuli) {
            const prompt = stimulus.prompt ?? "";
            assert.doesNotMatch(prompt, /\b(?:29262|12345)\b/);
            for (const match of prompt.matchAll(/work item(?: ID:)?\s*"?(\d+)/g)) {
                assert.equal(match[1], stimulus.name.startsWith("private-preview-") ? "35001" : "35000");
                explicitIds++;
            }
        }
    }
    assert.ok(explicitIds > 0);
    const privatePlan = planner.spec.stimuli.find(({ name }) => name === "private-preview-release-plan-no-pipeline-generation");
    assert.ok(privatePlan?.prompt?.includes("35001"));
    assert.deepEqual(privatePlan.graders?.find(({ type }) => type === "tool-calls")?.config?.disallowed,
        ["azsdk_run_generate_sdk", "azsdk_release_sdk"]);
});
