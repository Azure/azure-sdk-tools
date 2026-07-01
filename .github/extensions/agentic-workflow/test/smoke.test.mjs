// Smoke test for the only non-trivial pure logic in extension.mjs: arg parsing and the
// PHASE_RESULT sentinel parser. Run: npm test  (sets AW_SKIP_JOIN so importing the module does
// not try to join a live session host).
import { test } from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

process.env.AW_SKIP_JOIN = "1";
const {
    parsePhaseResult,
    parseKv,
    isTruthy,
    parseTaskArgs,
    initRun,
    slugify,
    runId,
    researchNotesComplete,
    nextPhase,
    phaseComplete,
    recordPhaseResult,
    writeState,
    listRuns,
    isDiffJudgeTarget,
    diffCritiqueRel,
    buildDiffJudgeInstruction,
    joinConfig,
} = await import("../extension.mjs");

const mkRun = () => fs.mkdtempSync(path.join(os.tmpdir(), "aw-run-"));
const w = (dir, rel, body = "x") => {
    const full = path.join(dir, rel);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, body);
};

test("parsePhaseResult reads pass/fail/needs_input and reason", () => {
    assert.equal(parsePhaseResult("work...\nPHASE_RESULT: pass").result, "pass");
    assert.equal(parsePhaseResult("PHASE_RESULT: fail — build broke").result, "fail");
    assert.equal(parsePhaseResult("PHASE_RESULT: fail — build broke").reason, "build broke");
    assert.equal(parsePhaseResult("PHASE_RESULT: needs_input - which db?").result, "needs_input");
    assert.equal(parsePhaseResult("PHASE_RESULT: needs_input - which db?").reason, "which db?");
    assert.equal(parsePhaseResult("PHASE_RESULT:pass").result, "pass");
});

test("parsePhaseResult defaults to fail when sentinel missing", () => {
    const r = parsePhaseResult("the agent forgot the sentinel");
    assert.equal(r.result, "fail");
    assert.match(r.reason, /no PHASE_RESULT sentinel/);
});

test("parseKv splits key:value pairs from free text", () => {
    const { kv, rest } = parseKv("from:research to:plan unattended:true Add CSV export");
    assert.equal(kv.from, "research");
    assert.equal(kv.to, "plan");
    assert.equal(kv.unattended, "true");
    assert.equal(rest.join(" "), "Add CSV export");
});

test("isTruthy treats bare flag, true/1/yes/on as true", () => {
    for (const v of ["", "true", "1", "yes", "on", "TRUE"]) assert.equal(isTruthy(v), true, v);
    for (const v of ["false", "0", "no", undefined]) assert.equal(isTruthy(v), false, String(v));
});

test("slugify produces a safe run-dir name", () => {
    assert.equal(slugify("Add CSV export!"), "add-csv-export");
    assert.equal(slugify("   "), "run");
    assert.equal(slugify("a".repeat(80)).length, 40);
});

test("runId appends a stable content hash and disambiguates slug collisions", () => {
    // Deterministic for a given task.
    assert.equal(runId("Add CSV export"), runId("Add CSV export"));
    assert.match(runId("Add CSV export"), /^add-csv-export-[0-9a-f]{8}$/);
    // Two tasks sharing the same 40-char slug prefix get distinct run ids.
    const a = "a".repeat(80);
    const b = "a".repeat(80) + " different tail";
    assert.equal(slugify(a), slugify(b));
    assert.notEqual(runId(a), runId(b));
});

test("researchNotesComplete requires a note for every sub-item", () => {
    const dir = mkRun();
    w(dir, "subitems.json", JSON.stringify({ items: [{ id: "one" }, { id: "two" }] }));
    w(dir, "research/one.md", "note");
    assert.equal(researchNotesComplete(dir), false, "missing two.md => incomplete");
    w(dir, "research/two.md", "note");
    assert.equal(researchNotesComplete(dir), true, "all notes present => complete");
    fs.rmSync(dir, { recursive: true, force: true });
});

test("phase continuity: a phase advances only after a recorded pass AND its artifact exists", () => {
    const dir = mkRun();
    // Fresh run starts at the first simple-flow phase.
    assert.equal(nextPhase(dir, true).id, "assumptions");

    // Artifact present but no recorded pass (partial failure) => phase is NOT complete.
    w(dir, "assumptions.md");
    assert.equal(phaseComplete(dir, nextPhase(dir, true)), false);
    assert.equal(nextPhase(dir, true).id, "assumptions", "partial artifact must not advance");

    // Recorded pass advances to the next phase.
    recordPhaseResult(dir, "assumptions", "pass");
    assert.equal(nextPhase(dir, true).id, "plan");

    // A recorded FAIL (even with the artifact written) does not advance.
    w(dir, "plan.md");
    recordPhaseResult(dir, "plan", "fail", "blocked");
    assert.equal(nextPhase(dir, true).id, "plan", "recorded fail must not advance");

    recordPhaseResult(dir, "plan", "pass");
    assert.equal(nextPhase(dir, true).id, "implement");

    w(dir, "execution-log.md");
    recordPhaseResult(dir, "implement", "pass");
    assert.equal(nextPhase(dir, true), null, "all simple-flow phases complete");
    fs.rmSync(dir, { recursive: true, force: true });
});

test("phase continuity: research-item needs coverage even with a recorded pass", () => {
    const dir = mkRun();
    // Fast-forward the earlier full-flow phases.
    w(dir, "specs/architecture.md");
    recordPhaseResult(dir, "research", "pass");
    w(dir, "assumptions.md");
    recordPhaseResult(dir, "assumptions", "pass");
    w(dir, "subitems.json", JSON.stringify({ items: [{ id: "one" }, { id: "two" }] }));
    recordPhaseResult(dir, "classify", "pass");
    assert.equal(nextPhase(dir, false).id, "research-item");

    // A recorded pass with only one of two notes must NOT advance (coverage gate).
    w(dir, "research/one.md", "note");
    recordPhaseResult(dir, "research-item", "pass");
    assert.equal(nextPhase(dir, false).id, "research-item", "incomplete coverage must not advance");

    w(dir, "research/two.md", "note");
    assert.equal(nextPhase(dir, false).id, "plan", "full coverage advances");
    fs.rmSync(dir, { recursive: true, force: true });
});

test("resume: listRuns rehydrates task/flow from state.json and resumes at the right phase", () => {
    const cwd0 = process.cwd();
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "aw-cwd-"));
    try {
        process.chdir(tmp);
        const runDir = path.join(tmp, ".aw", "my-task-deadbeef");
        fs.mkdirSync(runDir, { recursive: true });
        writeState(runDir, { task: "My task", simple: true });
        w(runDir, "assumptions.md");
        recordPhaseResult(runDir, "assumptions", "pass");

        const runs = listRuns();
        assert.equal(runs.length, 1);
        assert.equal(runs[0].task, "My task");
        assert.equal(runs[0].simple, true);
        // The resumed run continues from the first incomplete phase, not from the start.
        assert.equal(nextPhase(runs[0].dir, runs[0].simple).id, "plan");
    } finally {
        process.chdir(cwd0);
        fs.rmSync(tmp, { recursive: true, force: true });
    }
});

test("parseTaskArgs extracts task and simple flag", () => {
    assert.deepEqual(
        (({ task, simple }) => ({ task, simple }))(parseTaskArgs("Add CSV export")),
        { task: "Add CSV export", simple: false },
    );
    assert.deepEqual(
        (({ task, simple }) => ({ task, simple }))(parseTaskArgs("Fix bug simple")),
        { task: "Fix bug", simple: true },
    );
    assert.equal(parseTaskArgs("Fix bug", true).simple, true);
    const parsed = parseTaskArgs("to:plan Add CSV export");
    assert.equal(parsed.task, "Add CSV export");
    assert.equal(parsed.kv.to, "plan");
});

test("diff judge helpers recognize git diff targets and build critique instructions", () => {
    for (const arg of ["diff", "git-diff", "git diff", "changes", "code-changes", "--diff"]) {
        assert.equal(isDiffJudgeTarget(arg), true, arg);
    }
    assert.equal(isDiffJudgeTarget("plan.md"), false);

    assert.equal(
        diffCritiqueRel(new Date("2026-07-01T19:43:59.847Z")),
        "critiques/git-diff-20260701T194359Z.md",
    );
    const instr = buildDiffJudgeInstruction({
        cwd: "/repo",
        runDir: "/repo/.aw/my-run",
        critiqueRel: "critiques/git-diff-20260701T194359Z.md",
        task: "My task",
    });
    assert.match(instr, /git status --short/);
    assert.match(instr, /git diff HEAD --stat/);
    assert.match(instr, /git diff HEAD -- \. ':\(exclude\)\.aw'/);
    assert.match(instr, /staged and unstaged implementation changes/);
    assert.match(instr, /Do not change code/);
    assert.match(instr, /\/repo\/\.aw\/my-run\/critiques\/git-diff-20260701T194359Z\.md/);
});

test("rpi-judge advertises diff review", () => {
    const cmd = joinConfig.commands.find((c) => c.name === "rpi-judge");
    assert.match(cmd.description, /diff/);
});

test("initRun creates the run dir and state.json", () => {
    const cwd0 = process.cwd();
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "aw-init-"));
    try {
        process.chdir(tmp);
        initRun("My task", true);
        const runDir = path.join(tmp, ".aw", runId("My task"));
        assert.ok(fs.existsSync(path.join(runDir, "task.txt")), "task.txt written");
        const state = JSON.parse(fs.readFileSync(path.join(runDir, "state.json"), "utf8"));
        assert.equal(state.task, "My task");
        assert.equal(state.simple, true);
        // A freshly-initialized simple run starts at the first phase.
        assert.equal(nextPhase(runDir, true).id, "assumptions");
    } finally {
        process.chdir(cwd0);
        fs.rmSync(tmp, { recursive: true, force: true });
    }
});

test("joinConfig registers the six phase agents", () => {
    const names = joinConfig.customAgents.map((a) => a.name).sort();
    assert.deepEqual(names, [
        "rpi-assumptions", "rpi-classify", "rpi-implement", "rpi-plan", "rpi-research", "rpi-research-item",
    ].sort());
});

test("joinConfig gives every phase all tools", () => {
    const byName = Object.fromEntries(joinConfig.customAgents.map((a) => [a.name, a]));
    for (const name of Object.keys(byName)) {
        assert.equal(byName[name].tools, null, `${name} should inherit all tools`);
    }
});

test("joinConfig registers the command surface", () => {
    assert.equal(joinConfig.infiniteSessions.enabled, true);
    const cmds = joinConfig.commands.map((c) => c.name);
    for (const expected of ["rpi-start", "rpi-start-simple", "rpi-resume", "rpi-auto", "rpi-auto-simple", "rpi-continue", "rpi-pause", "rpi-judge", "rpi-autojudge", "rpi-redo", "rpi-status", "rpi-compact", "rpi-implement"]) {
        assert.ok(cmds.includes(expected), `missing /${expected}`);
    }
});
