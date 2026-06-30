// Smoke test for the only non-trivial pure logic in extension.mjs: arg parsing and the
// PHASE_RESULT sentinel parser. Run: npm test  (sets AW_SKIP_JOIN so importing the module does
// not try to join a live session host).
import { test } from "node:test";
import assert from "node:assert/strict";

process.env.AW_SKIP_JOIN = "1";
const { parsePhaseResult, parseKv, isTruthy, slugify } = await import("../extension.mjs");

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

const { joinConfig } = await import("../extension.mjs");

test("joinConfig registers the seven phase agents", () => {
    const names = joinConfig.customAgents.map((a) => a.name).sort();
    assert.deepEqual(names, [
        "aw-assumptions", "aw-classify", "aw-critique", "aw-implement", "aw-plan", "aw-research", "aw-research-item",
    ].sort());
});

test("joinConfig scopes tools: implement has all, read phases are restricted", () => {
    const byName = Object.fromEntries(joinConfig.customAgents.map((a) => [a.name, a]));
    assert.equal(byName["aw-implement"].tools, null); // all tools
    assert.ok(!byName["aw-research"].tools.includes("edit"));
    assert.ok(byName["aw-research"].tools.includes("bash")); // research gets read + shell
    assert.ok(!byName["aw-plan"].tools.includes("bash")); // pure read phase, no shell
});

test("joinConfig guards the default agent and registers the command surface", () => {
    assert.deepEqual(joinConfig.defaultAgent.excludedTools, ["edit", "create", "delete", "write"]);
    assert.equal(joinConfig.infiniteSessions.enabled, true);
    const cmds = joinConfig.commands.map((c) => c.name);
    for (const expected of ["aw-start", "aw-run", "aw-continue", "aw-pause", "aw-judge", "aw-redo", "aw-model", "aw-status", "aw-compact", "aw-implement"]) {
        assert.ok(cmds.includes(expected), `missing /${expected}`);
    }
});
