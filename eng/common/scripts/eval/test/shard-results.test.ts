// Verifies complete result staging, authoritative attempts and recovery after transport failures.
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { spawnSync } from "node:child_process";
import { test } from "node:test";
import { expectedShardsFromMatrix, latestShardAttempts, readShardAttempts, selectSummaryResults, stageShardResults } from "../lib/shard-results.ts";
import { getVallyShardVerdict } from "../lib/verdict.ts";

function setup(t) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "eval-artifacts-"));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    return root;
}

function invocation(root, name, { failed = false, summary = true, malformed = false, junit = true } = {}) {
    const directory = path.join(root, name);
    fs.mkdirSync(directory, { recursive: true });
    const records = [{ type: "trial-result", status: failed ? "error" : "success", itemId: name }];
    if (summary) records.push({ type: "run-summary", evals: [{ name, stimuliRun: 1, passed: !failed }] });
    fs.writeFileSync(path.join(directory, "results.jsonl"), records.map((entry) => JSON.stringify(entry)).join("\n") + (malformed ? '\n{"type":' : "\n"));
    if (junit) fs.writeFileSync(path.join(directory, "eval-results.junit.xml"),
        `<testsuites><testsuite name="example"><testcase name="${name}" time="1">${failed ? '<failure message="evaluation failed"/>' : ""}</testcase></testsuite></testsuites>`);
    return directory;
}

function artifact(root, shard, attempt, options = {}) {
    const source = invocation(root, `source-${shard}-${attempt}`, options);
    const output = path.join(root, "download", `eval-result-${shard}-${attempt}`);
    stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: shard, attempt });
    const evidence = path.join(root, "job-attempts.json");
    const timeline = fs.existsSync(evidence) ? readJson(evidence) : { schemaVersion: 1, valid: true, attempts: {} };
    if (!timeline.attempts[shard] || timeline.attempts[shard].attempt < attempt) timeline.attempts[shard] = { attempt, complete: true };
    fs.writeFileSync(evidence, JSON.stringify(timeline));
    return output;
}

function readJson(file) {
    return JSON.parse(fs.readFileSync(file, "utf8"));
}

function runCli(scriptName, args, environment = {}, cwd = undefined) {
    const script = fileURLToPath(new URL(`../${scriptName}`, import.meta.url));
    const env = { ...process.env, ...environment };
    delete env.NODE_TEST_CONTEXT;
    for (const key of Object.keys(env)) if (env[key] === undefined) delete env[key];
    const preload = [];
    if (scriptName === "build-eval-summary.ts" && args.includes("--selected-root")) {
        const root = path.dirname(args[args.indexOf("--results-root") + 1]);
        fs.mkdirSync(root, { recursive: true });
        const mock = path.join(root, "rendering-offline.mjs");
        fs.writeFileSync(mock, `globalThis.fetch = () => { throw new Error("Summary rendering cannot contact a service"); };`);
        delete env.SYSTEM_ACCESSTOKEN;
        preload.push("--import", pathToFileURL(mock).href);
        args = [...args, "--attempts-file", path.join(root, "job-attempts.json")];
    }
    const child = spawnSync(process.execPath, ["--experimental-strip-types", ...preload, script, ...args], {
        encoding: "utf8", env, cwd, timeout: 30_000, maxBuffer: 1024 * 1024,
    });
    assert.ifError(child.error);
    return child;
}

function runSummary(root, shards, options = {}) {
    const output = path.join(root, "summary", "eval-summary.md");
    const matrix = Object.hasOwn(options, "matrix") ? options.matrix :
        JSON.stringify(Object.fromEntries(shards.map((shardName) => [shardName, { shardName }])));
    const child = runCli("build-eval-summary.ts", [
        "--results-root", path.join(root, "download"), "--selected-root", path.join(root, "selected"),
        "--output-path", output,
    ], { TF_BUILD: "true", EVAL_EXPECTED_MATRIX: matrix, ...options.environment });
    return {
        child, markdown: fs.readFileSync(output, "utf8"),
        result: readJson(path.join(root, "summary", "eval-summary.json")),
        index: readJson(path.join(root, "download", "shard-index.json")),
    };
}

test("grader line discovery handles every name, model and shard without fixed line offsets", async () => {
    const { mapGraderLogLines } = await import("../lib/grader-log-lines.ts");
    const records = [
        { itemId: "first", evalName: "suite", stimulus: "rename-client-property", model: "model-a",
            gradeResult: { details: [{ name: "tool-calls requires edit|apply_patch", passed: false, evidence: "Required edit was not called" }] } },
        { itemId: "second", evalName: "suite", stimulus: "generate-sdk", model: "model-b",
            gradeResult: { details: [{ name: "output-matches [CreatedAt]", passed: true, evidence: "Pattern matched" },
                { name: "forbids wrong-tool", passed: false, evidence: "Disallowed tool was called" }] } },
    ];
    for (const prefix of [[], ["Condition evaluation", "Evaluating condition", "Result: True", ""]]) {
        const lines = [...prefix,
            "\u001b[1m\u2501\u2501\u2501 suite \u00b7 rename-client-property [model-a] \u2501\u2501\u2501\u001b[0m",
            "  Graders (0/1)", "  ----------------", "  \u2718 tool-calls requires edit|apply_patch  Required edit was not called", "  1 grader(s) failed.",
            "\u2501\u2501\u2501 suite \u00b7 generate-sdk [model-b] \u2501\u2501\u2501",
            "  Graders (1/2)", "  ----------------", "  \u2714 output-matches [CreatedAt]  Pattern matched",
            "  \u2718 forbids wrong-tool  Disallowed tool was called", "  1 grader(s) failed."];
        const mapped = mapGraderLogLines(records, lines);
        assert.equal(mapped.first.graderLines[records[0].gradeResult.details[0].name], prefix.length + 4);
        assert.equal(mapped.second.graderLines[records[1].gradeResult.details[0].name], prefix.length + 9);
        assert.equal(mapped.second.graderLines[records[1].gradeResult.details[1].name], prefix.length + 10);
        assert.equal(mapGraderLogLines([records[0]], [...lines, ...lines]).first, undefined,
            "Repeated indistinguishable trial logs must not invent a line");
        assert.equal(mapGraderLogLines([{ ...records[0], model: "another-model" }], lines).first, undefined);
    }
});

test("task display prefix derives condition, trace and dependency lines from the selected task", async () => {
    const { taskLogPrefixLineCount, taskLogDisplayLines } = await import("../lib/grader-log-lines.ts");
    const sourceLog = { jobId: "job", taskId: "task" };
    const task = { id: "task", parentId: "job", state: 2, result: 2, logId: 7, condition: "succeeded()" };
    const provider = { tasks: [task], jobs: [{ id: "job" }], jobDependencies: { job: [] }, phases: [] };
    assert.equal(taskLogPrefixLineCount(provider, sourceLog, 7), 8);
    assert.equal(taskLogPrefixLineCount({ ...provider, jobDependencies: {} }, sourceLog, 7), 8);
    assert.equal(taskLogPrefixLineCount({ ...provider, tasks: [{ ...task, condition: null }] }, sourceLog, 7), 5);
    const changed = { ...provider, tasks: [{ ...task, condition: "and(\ntrue,\ntrue)", message: "first\nsecond" }],
        jobDependencies: { job: ["prepare", "validate"] }, phases: [{ refName: "prepare", name: "Prepare\ninputs" }] };
    assert.equal(taskLogPrefixLineCount(changed, sourceLog, 7), 16);
    for (const invalid of [{ ...task, parentId: "other" }, { ...task, logId: 8 }, { ...task, state: 1 },
        { ...task, condition: "variables.x", originalCondition: "\nvariables.x" }]) {
        assert.equal(taskLogPrefixLineCount({ ...provider, tasks: [invalid] }, sourceLog, 7), undefined);
    }
    assert.equal(taskLogPrefixLineCount({ ...provider, tasks: [{ ...task, condition: "variables.x" }],
        variables: { x: "first\nsecond" } }, sourceLog, 7), undefined);
    assert.deepEqual(taskLogDisplayLines("##[group]Output\r\nfirst\r\n##[endgroup]\r\nsecond", 2),
        ["", "", "##[group]Output", "first", "second"]);
});

test("grouped trials share only their verified grader aggregate line across repeated trials", async () => {
    const { mapGraderLogLines } = await import("../lib/grader-log-lines.ts");
    const records = [true, false, true].map((passed, index) => ({ itemId: String(index), evalName: "suite",
        stimulus: "scenario", model: "model", gradeResult: { details: [{ name: "grader [a|b]", passed }] } }));
    const lines = ["\u2501\u2501\u2501 suite \u00b7 scenario [model] \u2501\u2501\u2501", "Graders", "\u2718 grader [a|b]  2/3"];
    const mapped = mapGraderLogLines(records, lines);
    for (const record of records) assert.equal(mapped[record.itemId].graderLines["grader [a|b]"], 3);
    assert.equal(mapGraderLogLines(records, [...lines, ...lines])["0"], undefined);
    assert.equal(mapGraderLogLines(records.slice(1), lines)["1"], undefined);
});

test("repeated grader names preserve unique locations and select only an unambiguous failing occurrence", async () => {
    const { mapGraderLogLines } = await import("../lib/grader-log-lines.ts");
    const record = { itemId: "trial", evalName: "suite", stimulus: "conversation", model: "model", gradeResult: { details: [
        { name: "repeated", passed: true, evidence: "Matched" }, { name: "unique", passed: true, evidence: "Complete" },
        { name: "repeated", passed: true, evidence: "Matched" },
    ] } };
    const lines = ["\u2501\u2501\u2501 suite \u00b7 conversation [model] \u2501\u2501\u2501", "Graders", "------",
        "\u2714 repeated #1  Matched", "\u2714 unique  Complete", "\u2714 repeated #2  Matched"];
    const mapped = mapGraderLogLines([record], lines).trial;
    assert.equal(mapped.line, 1);
    assert.equal(mapped.graderLines.unique, 5);
    assert.equal(mapped.graderLines.repeated, undefined);
    const failing = structuredClone(record);
    failing.gradeResult.details[2] = { name: "repeated", passed: false, evidence: "Missing tool" };
    assert.equal(mapGraderLogLines([failing], [...lines.slice(0, -1), "\u2718 repeated #2  Missing tool"])
        .trial.graderLines.repeated, 6);
});

test("timeline capture maps every selected shard, retains retry identity and tolerates unavailable logs", async t => {
    const root = setup(t), matrix = {}, records = [], tasks = [], jobs = [], jobDependencies = {};
    const expected = [];
    for (const [index, condition] of ["succeeded()", "and(\ntrue,\ntrue)"].entries()) {
        const shard = `area_${index}`, directory = artifact(root, shard, 2);
        const jobId = `11111111-1111-4111-8111-11111111111${index}`, taskId = `22222222-2222-4222-8222-22222222222${index}`;
        const trial = { type: "trial-result", status: "success", itemId: `trial-${index}`, evalName: "suite",
            stimulus: `scenario-${index}`, model: "model", gradeResult: { details: [{ name: `grader [${index}|x]`, passed: false, evidence: "Missing tool" }] } };
        fs.writeFileSync(path.join(directory, "results.jsonl"), JSON.stringify(trial) + '\n{"type":"run-summary","evals":[{"name":"suite"}]}\n');
        const raw = fs.readFileSync(path.join(directory, "results.jsonl"), "utf8");
        matrix[`leg_${index}`] = { shardName: shard }; jobs.push({ id: jobId });
        if (index) jobDependencies[jobId] = [];
        records.push({ type: "Job", identifier: `Eval.RunShard.leg_${index}`, id: jobId, attempt: 2, state: "completed", result: "failed" },
            { type: "Task", name: `vally eval ${shard}`, id: taskId, parentId: jobId, attempt: 2, state: "completed", log: { id: index + 7, url: "https://untrusted.invalid/log" } });
        tasks.push({ id: taskId, parentId: jobId, state: 2, result: 2, logId: index + 7, condition });
        expected.push({ directory, raw, trial, shard, line: 12 + index * 2 });
    }
    const environment = { ...timelineEnv, EVAL_EXPECTED_MATRIX: JSON.stringify(matrix) };
    const fetchImpl = async (url, options) => {
        assert.equal(new URL(url).origin, "https://dev.azure.com");
        assert.equal(options.redirect, "error");
        assert.equal(options.headers.authorization, "Bearer fixture-token");
        if (String(url).includes("/timeline?")) return Response.json({ records });
        if (String(url).includes("/Contribution/")) return Response.json({ dataProviders: {
            "ms.vss-build-web.run-logs-data-provider": { buildId: 1001, projectId: timelineEnv.SYSTEM_TEAMPROJECTID, tasks, jobs, jobDependencies },
        } });
        assert.equal(options.headers.accept, "text/plain");
        const index = Number(/\/logs\/(\d+)/.exec(String(url))[1]) - 7;
        return new Response([`\u2501\u2501\u2501 suite \u00b7 scenario-${index} [model] \u2501\u2501\u2501`, "Graders", "------",
            `\u2718 grader [${index}|x]  Missing tool`].join("\n"));
    };
    const evidence = await readShardAttempts(environment, { fetchImpl, resultsRoot: path.join(root, "download") });
    for (const entry of expected) {
        assert.equal(evidence.attempts[entry.shard].logLines, "source-log-lines.json");
        const mapped = readJson(path.join(entry.directory, "source-log-lines.json"));
        assert.equal(mapped.trials[entry.trial.itemId].graderLines[entry.trial.gradeResult.details[0].name], entry.line);
        assert.equal(fs.readFileSync(path.join(entry.directory, "results.jsonl"), "utf8"), entry.raw);
    }
    const selected = selectSummaryResults({ resultsRoot: path.join(root, "download"), selectedRoot: path.join(root, "selected"),
        expectedShards: expected.map(entry => entry.shard), jobAttempts: evidence });
    assert.equal(selected.complete, true);
    assert.ok(selected.shardInput.attempts.every(attempt => attempt.attempt === 2 && attempt.logLines === "source-log-lines.json"));
    const unavailable = await readShardAttempts(environment, { resultsRoot: path.join(root, "download"), fetchImpl: (url, options) =>
        String(url).includes("/logs/") ? new Response("denied", { status: 403 }) : fetchImpl(url, options) });
    assert.equal(unavailable.valid, true);
    assert.ok(Object.values(unavailable.attempts).every(attempt => attempt.complete && attempt.sourceLog && !attempt.logLines));
});

test("selected shard attempts retain their exact evaluation task IDs, never another job or retry", () => {
    const jobId = "11111111-1111-4111-8111-111111111111";
    const taskId = "22222222-2222-4222-8222-222222222222";
    const oldJobId = "33333333-3333-4333-8333-333333333333";
    const matrix = { leg: { shardName: "area_test" } };
    const records = [
        { type: "Job", identifier: "Eval.RunShard.leg", id: oldJobId, attempt: 1, state: "completed", result: "failed" },
        { type: "Job", identifier: "Eval.RunShard.leg", id: jobId, attempt: 2, state: "completed", result: "failed" },
        { type: "Task", name: "vally eval area_test", id: oldJobId, parentId: oldJobId, attempt: 1, state: "completed" },
        { type: "Task", name: "Collect area_test transcripts (on failure)", id: oldJobId, parentId: jobId, attempt: 2, state: "completed" },
        { type: "Task", name: "vally eval area_test", id: taskId, parentId: jobId, attempt: 2, state: "completed" },
    ];
    const selected = latestShardAttempts({ records }, matrix).attempts.area_test;
    assert.deepEqual(selected.sourceLog, { jobId, taskId });
    assert.equal(selected.attempt, 2);
    for (const name of ["vally eval area_test", "vally eval area_test (authenticated)"]) {
        const tasks = records.map(record => record.id === taskId ? { ...record, name } : record);
        assert.deepEqual(latestShardAttempts({ records: tasks }, matrix).attempts.area_test.sourceLog, { jobId, taskId });
    }
    for (const tasks of [records.filter(record => record.id !== taskId),
        records.map(record => record.id === taskId ? { ...record, attempt: 1 } : record),
        [...records, { ...records.at(-1), id: oldJobId }]]) {
        assert.equal(latestShardAttempts({ records: tasks }, matrix).attempts.area_test.sourceLog, undefined);
    }
});

test("expected shards come from the full Prepare matrix, not arrived artifacts or matrix keys", () => {
    const matrix = { secondJob: { shardName: "area_b", evalArgs: "-e b.eval.yaml" }, firstJob: { shardName: "area_a" } };
    assert.deepEqual(expectedShardsFromMatrix(matrix), ["area_a", "area_b"]);
    assert.deepEqual(expectedShardsFromMatrix(JSON.stringify(matrix)), ["area_a", "area_b"]);
    for (const invalid of [undefined, "not JSON", {}, [], null, true, 1, { a: null }, { a: { shardName: "../escape" } },
        { a: { shardName: "" } }, { a: { shardName: "x".repeat(201) } }, { a: { shardName: "same" }, b: { shardName: "same" } }]) {
        assert.throws(() => expectedShardsFromMatrix(invalid));
    }
});

test("completed failing evaluations stage exact raw bytes and all JUnit, but no unrelated files", (t) => {
    const root = setup(t);
    const source = invocation(root, "source", { failed: true });
    fs.writeFileSync(path.join(source, "debug.log"), "not bundled");
    fs.writeFileSync(path.join(source, "mcp.dll"), "not bundled");
    fs.mkdirSync(path.join(source, "nested"));
    fs.copyFileSync(path.join(source, "eval-results.junit.xml"), path.join(source, "nested", "eval-results.junit.xml"));
    const output = path.join(root, "staged");
    const result = stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: "area_test", attempt: 2 });
    assert.deepEqual(result, { schemaVersion: 1, shard: "area_test", attempt: 2, complete: true, trials: 1, reason: null });
    assert.deepEqual(readJson(path.join(output, "shard.json")), result);
    assert.deepEqual(fs.readdirSync(output).sort(), ["junit", "results.jsonl", "shard.json"]);
    assert.deepEqual(fs.readdirSync(path.join(output, "junit")), ["0.junit.xml", "1.junit.xml"]);
    assert.deepEqual(fs.readFileSync(path.join(output, "results.jsonl")), fs.readFileSync(path.join(source, "results.jsonl")));
});

test("missing, interrupted and corrupt invocations publish incomplete markers", (t) => {
    const root = setup(t);
    for (const [index, options] of [{ summary: false }, { malformed: true }, { junit: false }].entries()) {
        const source = invocation(root, `source-${index}`, options);
        const output = path.join(root, `stage-${index}`);
        const result = stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: "test", attempt: 1 });
        assert.equal(result.complete, false);
        assert.equal(result.trials, 1);
        assert.ok(result.reason);
        assert.deepEqual(readJson(path.join(output, "shard.json")), result);
        assert.deepEqual(fs.readFileSync(path.join(output, "results.jsonl")), fs.readFileSync(path.join(source, "results.jsonl")));
    }
    const output = path.join(root, "empty");
    const empty = stageShardResults({ resultsRoot: path.join(root, "not-created"), outputDirectory: output, shardName: "test", attempt: 1 });
    assert.equal(empty.complete, false);
    assert.equal(empty.trials, 0);
    assert.deepEqual(fs.readdirSync(output).sort(), ["junit", "shard.json"]);
});

test("non-object JSONL and empty final summaries are incomplete, and a trial invalidates an earlier summary", (t) => {
    const root = setup(t);
    const suffixes = ["null", "[]", "42", '{"type":"run-summary","evals":[]}',
        '{"type":"run-summary","evals":null}', '{"type":"trial-result","status":"success"}'];
    for (const [index, suffix] of suffixes.entries()) {
        const source = invocation(root, `source-${index}`);
        fs.appendFileSync(path.join(source, "results.jsonl"), suffix + "\n");
        const result = stageShardResults({ resultsRoot: source, outputDirectory: path.join(root, `staged-${index}`), shardName: "test", attempt: 1 });
        assert.equal(result.complete, false);
    }
});

test("untyped trial records, CRLF and blank lines retain the reference completion semantics", (t) => {
    const root = setup(t);
    const source = invocation(root, "source");
    fs.writeFileSync(path.join(source, "results.jsonl"), '\r\n{"status":"success"}\r\n \r\n{"type":"run-summary","evals":[{"name":"legacy"}]}\r\n');
    const result = stageShardResults({ resultsRoot: source, outputDirectory: path.join(root, "staged"), shardName: "test", attempt: 1 });
    assert.equal(result.complete, true);
    assert.equal(result.trials, 1);
});

test("staging selects the same newest invocation as the shard verdict", (t) => {
    const root = setup(t);
    const old = invocation(root, "runs/old");
    const latest = invocation(root, "runs/new", { failed: true });
    fs.utimesSync(path.join(old, "results.jsonl"), 1, 1);
    fs.utimesSync(path.join(latest, "results.jsonl"), 2, 2);
    const output = path.join(root, "stage");
    assert.equal(getVallyShardVerdict({ resultsDir: path.join(root, "runs") }).passed, false);
    stageShardResults({ resultsRoot: path.join(root, "runs"), outputDirectory: output, shardName: "test", attempt: 1 });
    assert.deepEqual(fs.readFileSync(path.join(output, "results.jsonl")), fs.readFileSync(path.join(latest, "results.jsonl")));
    assert.deepEqual(fs.readFileSync(path.join(output, "junit", "0.junit.xml")), fs.readFileSync(path.join(latest, "eval-results.junit.xml")));
});

test("equal-mtime invocation ties use the verdict's path order, not localeCompare", (t) => {
    const root = setup(t);
    const first = invocation(root, "runs/Z");
    const last = invocation(root, "runs/a", { failed: true });
    for (const directory of [first, last]) fs.utimesSync(path.join(directory, "results.jsonl"), 1, 1);
    const output = path.join(root, "stage");
    assert.equal(getVallyShardVerdict({ resultsDir: path.join(root, "runs") }).passed, false);
    stageShardResults({ resultsRoot: path.join(root, "runs"), outputDirectory: output, shardName: "test", attempt: 1 });
    assert.deepEqual(fs.readFileSync(path.join(output, "results.jsonl")), fs.readFileSync(path.join(last, "results.jsonl")));
});

test("a corrupt newest invocation never falls back to older complete output", (t) => {
    const root = setup(t);
    const old = invocation(root, "runs/old");
    const latest = invocation(root, "runs/new", { malformed: true });
    fs.utimesSync(path.join(old, "results.jsonl"), 1, 1);
    fs.utimesSync(path.join(latest, "results.jsonl"), 2, 2);
    const output = path.join(root, "stage");
    const result = stageShardResults({ resultsRoot: path.join(root, "runs"), outputDirectory: output, shardName: "test", attempt: 1 });
    assert.equal(result.complete, false);
    assert.deepEqual(fs.readFileSync(path.join(output, "results.jsonl")), fs.readFileSync(path.join(latest, "results.jsonl")));
});

test("staging requires an empty destination and does not delete existing contents", (t) => {
    const root = setup(t);
    const source = invocation(root, "source");
    const output = path.join(root, "staged");
    fs.mkdirSync(output);
    fs.writeFileSync(path.join(output, "keep.txt"), "keep me");
    assert.throws(() => stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: "test", attempt: 1 }), /must be empty/);
    assert.deepEqual(fs.readdirSync(output), ["keep.txt"]);
    assert.equal(fs.readFileSync(path.join(output, "keep.txt"), "utf8"), "keep me");
});

test("invalid shard identities or attempts are rejected before creating an output", (t) => {
    const root = setup(t);
    const output = path.join(root, "staged");
    for (const shardName of ["", "../escape", "a/b", "a\\b", "a\n", "x".repeat(201), undefined]) {
        assert.throws(() => stageShardResults({ resultsRoot: root, outputDirectory: output, shardName, attempt: 1 }));
    }
    for (const attempt of [0, -1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1]) {
        assert.throws(() => stageShardResults({ resultsRoot: root, outputDirectory: output, shardName: "test", attempt }));
    }
    assert.equal(fs.existsSync(output), false);
});

test("symbolic-link destinations are not treated as owned empty directories", (t) => {
    const root = setup(t);
    const source = invocation(root, "source");
    const target = path.join(root, "target");
    const output = path.join(root, "link");
    fs.mkdirSync(target);
    fs.symlinkSync(target, output, "junction");
    assert.throws(() => stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: "test", attempt: 1 }), /symbolic link/);
    assert.deepEqual(fs.readdirSync(target), []);
});

test("oversized raw files are not read or copied, but JUnit and an incomplete marker survive", (t) => {
    const root = setup(t);
    const source = invocation(root, "source");
    const fd = fs.openSync(path.join(source, "results.jsonl"), "r+");
    try { fs.ftruncateSync(fd, 128 * 1024 * 1024 + 1); } finally { fs.closeSync(fd); }
    const output = path.join(root, "stage");
    const result = stageShardResults({ resultsRoot: source, outputDirectory: output, shardName: "test", attempt: 1 });
    assert.equal(result.complete, false);
    assert.match(result.reason, /oversized/);
    assert.equal(fs.existsSync(path.join(output, "results.jsonl")), false);
    assert.deepEqual(fs.readdirSync(path.join(output, "junit")), ["0.junit.xml"]);
    assert.equal(readJson(path.join(output, "shard.json")).complete, false);
});

test("summary selects latest expected attempts once; completed failures remain eligible for publication", (t) => {
    const root = setup(t);
    artifact(root, "area_a", 1, { failed: true });
    artifact(root, "area_a", 2);
    artifact(root, "area_b", 1, { failed: true });
    artifact(root, "unrequested", 9);
    const { child, markdown, result, index } = runSummary(root, ["area_b", "area_a"]);
    assert.equal(child.status, 0, child.stderr);
    assert.match(child.stdout, /##vso\[task.setvariable variable=EvalSummaryComplete\]true/);
    assert.deepEqual(result, {
        schemaVersion: 1, complete: true,
        shards: [{ shard: "area_a", attempt: 2, complete: true, reason: null }, { shard: "area_b", attempt: 1, complete: true, reason: null }],
        totals: { scenarios: 2, failed: 1, skipped: 0 },
    });
    assert.match(markdown, /results — FAILED/);
    assert.doesNotMatch(markdown, /Selected shard attempts|\| Shard \| Attempt \| Result data \|/);
    assert.match(markdown, /\| Shard \| Result \| Pass rate \| Passed \| Failed \| Skipped \| Time \(s\) \|/);
    assert.deepEqual(fs.readdirSync(path.join(root, "selected")).sort(), ["eval-result-area_a-2", "eval-result-area_b-1"]);
    assert.deepEqual(index, {
        schemaVersion: 1, complete: true, expectedShards: ["area_a", "area_b"],
        attempts: [{ shard: "area_a", attempt: 2, directory: "eval-result-area_a-2" }, { shard: "area_b", attempt: 1, directory: "eval-result-area_b-1" }],
    });
});

test("failure table uses every recorded failed check and only selected task-line evidence", (t) => {
    const sourceLog = { jobId: "11111111-1111-4111-8111-111111111111", taskId: "22222222-2222-4222-8222-222222222222" };
    const oldLog = { jobId: "33333333-3333-4333-8333-333333333333", taskId: "44444444-4444-4444-8444-444444444444" };
    for (const validLines of [true, false]) {
        const root = setup(t);
        artifact(root, "area_a", 1, { failed: true });
        const directory = artifact(root, "area_a", 2, { failed: true });
        const file = path.join(directory, "results.jsonl");
        const records = fs.readFileSync(file, "utf8").trim().split("\n").map(line => JSON.parse(line));
        const record = records.find(item => item.type === "trial-result");
        record.stimulus = record.itemId;
        record.model = "model";
        record.sourceLog = { ...oldLog, graderLines: { "failed tool": 999 } };
        record.gradeResult = { details: [
            { name: "failed tool", passed: false, evidence: "Required tool was not called" },
            { name: "failed skill", passed: false, evidence: "Required skill was not called" },
        ] };
        fs.writeFileSync(file, records.map(item => JSON.stringify(item)).join("\n") + "\n");
        const evidencePath = path.join(root, "job-attempts.json");
        const evidence = readJson(evidencePath);
        evidence.attempts.area_a.sourceLog = sourceLog;
        evidence.attempts.area_a.logLines = "source-log-lines.json";
        fs.writeFileSync(evidencePath, JSON.stringify(evidence));
        fs.writeFileSync(path.join(directory, "source-log-lines.json"), JSON.stringify({ schemaVersion: 1,
            ...(validLines ? sourceLog : oldLog), trials: { [record.itemId]: { line: 250,
                graderLines: { "failed tool": 259, "failed skill": 261 } } } }));
        const { child, markdown, result } = runSummary(root, ["area_a"], { environment: {
            SYSTEM_COLLECTIONURI: "https://dev.azure.com/azure-sdk/", SYSTEM_TEAMPROJECT: "internal", BUILD_BUILDID: "12345",
        } });
        assert.equal(child.status, 0, child.stderr);
        assert.equal(result.complete, true);
        assert.match(child.stdout, /task.addattachment type=Distributedtask.Core.Summary;name=Evaluation results;/);
        assert.match(markdown, /\| Scenario \| Failed check \| Trials passed \| Evidence \|/);
        assert.ok(markdown.includes("failed tool") && markdown.includes("failed skill"));
        const urls = [...markdown.matchAll(/\]\((https:\/\/dev\.azure\.com\/[^)]+)\)/g)].map(match => new URL(match[1]));
        assert.ok(urls.length >= 6);
        assert.ok(urls.every(url => url.searchParams.get("j") === sourceLog.jobId && url.searchParams.get("t") === sourceLog.taskId));
        if (validLines) assert.deepEqual([...new Set(urls.map(url => url.searchParams.get("l")))].sort(), ["259", "261"]);
        else assert.ok(urls.every(url => !url.searchParams.has("l")));
        assert.doesNotMatch(markdown, /999|33333333-3333/);
    }
});

test("a later incomplete attempt and a missing shard never fall back to complete older artifacts", (t) => {
    const root = setup(t);
    artifact(root, "area_a", 1);
    artifact(root, "area_a", 2, { junit: false });
    const { child, markdown, result, index } = runSummary(root, ["area_a", "area_missing"]);
    assert.equal(child.status, 1);
    assert.match(child.stdout, /##vso\[task.setvariable variable=EvalSummaryComplete\]false/);
    assert.equal(result.complete, false);
    assert.equal(index.complete, false);
    assert.match(markdown, /results — INCOMPLETE/);
    assert.doesNotMatch(markdown, /results — PASSED/);
    assert.equal(result.shards[0].attempt, 2);
    assert.equal(result.shards[1].attempt, null);
    assert.equal(result.totals.scenarios, 0);
    assert.deepEqual(index.expectedShards, ["area_a", "area_missing"]);
    assert.deepEqual(index.attempts, [{ shard: "area_a", attempt: 2, directory: "eval-result-area_a-2" }]);
});

test("attempt selection is numeric and includes the newest directory even when it is empty", (t) => {
    const root = setup(t);
    artifact(root, "area_a", 9);
    fs.mkdirSync(path.join(root, "download", "eval-result-area_a-10"));
    fs.writeFileSync(path.join(root, "job-attempts.json"), JSON.stringify({ schemaVersion: 1, valid: true,
        attempts: { area_a: { attempt: 10, complete: true } } }));
    const { child, result, index } = runSummary(root, ["area_a"]);
    assert.equal(child.status, 1);
    assert.equal(result.shards[0].attempt, 10);
    assert.equal(result.totals.scenarios, 0);
    assert.equal(index.attempts[0].attempt, 10);
    assert.deepEqual(fs.readdirSync(path.join(root, "selected")), ["eval-result-area_a-10"]);
});

test("zero parsed JUnit cases make that shard incomplete even when another shard has results", (t) => {
    const root = setup(t);
    const output = artifact(root, "area_a", 1);
    fs.writeFileSync(path.join(output, "junit", "0.junit.xml"), "<testsuites />");
    artifact(root, "area_b", 1);
    const { child, result, index, markdown } = runSummary(root, ["area_a", "area_b"]);
    assert.equal(child.status, 1);
    assert.equal(result.complete, false);
    assert.equal(index.complete, false);
    assert.equal(result.totals.scenarios, 1);
    assert.match(result.shards[0].reason, /no evaluation testcases/);
    assert.equal(result.shards[1].complete, true);
    assert.match(markdown, /results — INCOMPLETE/);
});

test("bad completion metadata cannot hide available diagnostic JUnit", (t) => {
    const root = setup(t);
    const base = { schemaVersion: 1, shard: "area_a", attempt: 1, complete: true, trials: 1 };
    const markers = ["not JSON", "null", JSON.stringify({}), JSON.stringify({ ...base, schemaVersion: 2 }),
        JSON.stringify({ ...base, shard: "other" }), JSON.stringify({ ...base, attempt: 2 }),
        JSON.stringify({ ...base, complete: "true" }), JSON.stringify({ ...base, trials: 0 }),
        JSON.stringify({ ...base, trials: 1.5 }), JSON.stringify({ ...base, trials: 2 }),
        JSON.stringify({ ...base, padding: " ".repeat(64 * 1024) })];
    for (const [index, marker] of markers.entries()) {
        const directory = path.join(root, `case-${index}`);
        const output = artifact(directory, "area_a", 1);
        fs.writeFileSync(path.join(output, "shard.json"), marker);
        const selection = selectSummaryResults({ resultsRoot: path.join(directory, "download"), selectedRoot: path.join(directory, "selected"),
            expectedShards: ["area_a"], jobAttempts: readJson(path.join(directory, "job-attempts.json")) });
        assert.equal(selection.complete, false);
        assert.deepEqual(fs.readdirSync(path.join(directory, "selected", "eval-result-area_a-1", "junit")), ["0.junit.xml"]);
    }
});

test("missing or corrupted raw results override a complete marker without losing diagnostic cases", (t) => {
    const root = setup(t);
    for (const corrupt of [false, true]) {
        const directory = path.join(root, String(corrupt));
        artifact(directory, "area_a", 1);
        const output = artifact(directory, "area_a", 2);
        if (corrupt) fs.appendFileSync(path.join(output, "results.jsonl"), "not JSON\n");
        else fs.unlinkSync(path.join(output, "results.jsonl"));
        const { child, result, index } = runSummary(directory, ["area_a"]);
        assert.equal(child.status, 1);
        assert.equal(result.complete, false);
        assert.equal(result.shards[0].attempt, 2);
        assert.equal(result.totals.scenarios, 1);
        assert.equal(index.complete, false);
    }
});

test("legacy-layout JUnit is diagnostic only, and never causes fallback to an older attempt", (t) => {
    const root = setup(t);
    artifact(root, "area_a", 1);
    const latest = path.join(root, "download", "eval-result-area_a-2");
    fs.mkdirSync(latest);
    fs.writeFileSync(path.join(latest, "eval-results.junit.xml"), '<testsuite><testcase name="latest"><failure /></testcase></testsuite>');
    fs.writeFileSync(path.join(root, "job-attempts.json"), JSON.stringify({ schemaVersion: 1, valid: true,
        attempts: { area_a: { attempt: 2, complete: true } } }));
    const { child, result, index, markdown } = runSummary(root, ["area_a"]);
    assert.equal(child.status, 1);
    assert.equal(result.complete, false);
    assert.deepEqual(result.totals, { scenarios: 1, failed: 1, skipped: 0 });
    assert.equal(index.attempts[0].attempt, 2);
    assert.match(markdown, /\| latest<br>.*\| Check details unavailable \| 0\/1 \|/);
    assert.equal(fs.existsSync(path.join(root, "selected", "eval-result-area_a-1")), false);
});

test("failed Prepare retains incomplete summary contracts without inferring expected shards", (t) => {
    const root = setup(t);
    for (const [index, matrix] of [undefined, "", "not JSON", "{}", "[]"].entries()) {
        const directory = path.join(root, `case-${index}`);
        artifact(directory, "arrived", 1);
        const { child, markdown, result, index: shardIndex } = runSummary(directory, [], { matrix });
        assert.equal(child.status, 1);
        assert.equal(result.complete, false);
        assert.equal(result.totals.scenarios, 0);
        assert.match(markdown, /results — INCOMPLETE/);
        assert.match(markdown, /check the Prepare stage/);
        assert.deepEqual(shardIndex, { schemaVersion: 1, complete: false, expectedShards: [], attempts: [] });
        assert.deepEqual(fs.readdirSync(path.join(directory, "selected")), []);
    }
});

test("a missing artifact root reports every expected shard missing", (t) => {
    const root = setup(t);
    const { child, result, index } = runSummary(root, ["area_a", "area_b"]);
    assert.equal(child.status, 1);
    assert.equal(result.complete, false);
    assert.equal(result.totals.scenarios, 0);
    assert.ok(result.shards.every((entry) => entry.attempt === null));
    assert.deepEqual(index.expectedShards, ["area_a", "area_b"]);
    assert.deepEqual(index.attempts, []);
});

test("summary cannot publish when timeline evidence is unavailable, invalid or corrupt", (t) => {
    const root = setup(t);
    for (const mode of ["invalid", "missing", "corrupt"]) {
        const directory = path.join(root, mode);
        artifact(directory, "area_a", 1);
        const attempts = path.join(directory, "job-attempts.json");
        if (mode === "invalid") fs.writeFileSync(attempts, JSON.stringify({ valid: false }));
        if (mode === "missing") fs.unlinkSync(attempts);
        if (mode === "corrupt") fs.writeFileSync(attempts, "not JSON");
        const output = path.join(directory, "summary", "eval-summary.md");
        const child = runCli("build-eval-summary.ts", ["--results-root", path.join(directory, "download"),
            "--selected-root", path.join(directory, "selected"), "--output-path", output], {
            TF_BUILD: "true", EVAL_EXPECTED_MATRIX: JSON.stringify({ a: { shardName: "area_a" } }),
        });
        assert.equal(child.status, 1); assert.match(child.stdout, /EvalSummaryComplete\]false/);
        assert.equal(readJson(path.join(directory, "download", "shard-index.json")).complete, false);
        assert.equal(readJson(path.join(directory, "summary", "eval-summary.json")).totals.scenarios, 0);
        assert.deepEqual(fs.readdirSync(path.join(directory, "selected")), []);
        assert.deepEqual(readJson(path.join(directory, "download", "shard-index.json")).attempts, []);
        assert.match(fs.readFileSync(output, "utf8"), /timeline/);
        assert.equal(fs.existsSync(path.join(directory, "summary", "job-attempts.json")), false, "Rendering never fabricates timeline evidence");
    }
});

test("selection rejects stale destinations and never treats zero expected shards as complete", (t) => {
    const root = setup(t);
    artifact(root, "area_a", 1);
    const selectedRoot = path.join(root, "selected");
    fs.mkdirSync(selectedRoot);
    fs.writeFileSync(path.join(selectedRoot, "keep.txt"), "keep me");
    assert.throws(() => selectSummaryResults({ resultsRoot: path.join(root, "download"), selectedRoot, expectedShards: ["area_a"] }), /must be empty/);
    assert.equal(fs.readFileSync(path.join(selectedRoot, "keep.txt"), "utf8"), "keep me");
    assert.equal(fs.existsSync(path.join(root, "download", "shard-index.json")), false);
    const selection = selectSummaryResults({ resultsRoot: path.join(root, "download"), selectedRoot: path.join(root, "empty-selection"), expectedShards: [] });
    assert.equal(selection.complete, false);
    assert.equal(selection.shardInput.complete, false);
    assert.deepEqual(selection.shardInput.attempts, []);
});

test("ambiguous numeric attempt names and invalid attempts are rejected", (t) => {
    const root = setup(t);
    for (const [index, names] of [["01", "1", "2"], ["0"], ["9007199254740992"]].entries()) {
        const directory = path.join(root, `case-${index}`);
        const resultsRoot = path.join(directory, "download");
        for (const name of names) fs.mkdirSync(path.join(resultsRoot, `eval-result-area_a-${name}`), { recursive: true });
        assert.throws(() => selectSummaryResults({ resultsRoot, selectedRoot: path.join(directory, "selected"), expectedShards: ["area_a"] }), /artifact attempt/i);
    }
});

test("staging CLI logs counts and sanitized warnings, never the raw invalid record", (t) => {
    const root = setup(t);
    const source = invocation(root, "source");
    const privateText = "SYNTHETIC_RAW_RECORD_DO_NOT_LOG";
    fs.appendFileSync(path.join(source, "results.jsonl"), privateText);
    const output = path.join(root, "stage");
    const child = runCli("lib/shard-results.ts", ["--results-root", source, "--output-directory", output, "--shard-name", "area_test", "--attempt", "3"]);
    assert.equal(child.status, 0, child.stderr);
    assert.match(child.stdout, /Staged area_test attempt 3: 1 trials; incomplete/);
    assert.match(child.stdout, /##vso\[task.logissue type=warning\]/);
    assert.ok(!(child.stdout + child.stderr).includes(privateText));
    assert.equal(readJson(path.join(output, "shard.json")).complete, false);
});

test("summary CLI without selected-root preserves legacy JUnit-only behavior and default output", (t) => {
    const root = setup(t);
    const results = path.join(root, "download", "eval-result-legacy");
    fs.mkdirSync(results, { recursive: true });
    fs.writeFileSync(path.join(results, "junit.xml"), '<testsuite><testcase name="ok" /></testsuite>');
    const child = runCli("build-eval-summary.ts", ["--results-root", path.join(root, "download")],
        { TF_BUILD: "true", EVAL_EXPECTED_MATRIX: "invalid matrix is irrelevant in legacy mode" }, root);
    assert.equal(child.status, 0, child.stderr);
    assert.match(fs.readFileSync(path.join(root, "eval-summary.md"), "utf8"), /results — PASSED/);
    assert.match(child.stdout, /##vso\[task.addattachment type=Distributedtask.Core.Summary;name=Evaluation results;\]/);
    assert.doesNotMatch(child.stdout, /EvalSummaryComplete/);
    assert.equal(fs.existsSync(path.join(root, "eval-summary.json")), false);
    assert.equal(fs.existsSync(path.join(root, "download", "shard-index.json")), false);
});

test("a selected-root flag without a value cannot silently fall back to legacy mode", (t) => {
    const root = setup(t);
    const child = runCli("build-eval-summary.ts", ["--results-root", root, "--selected-root"], { TF_BUILD: "true" }, root);
    assert.equal(child.status, 1);
    assert.match(child.stderr, /Missing value for --selected-root/);
    assert.match(child.stdout, /##vso\[task.setvariable variable=EvalSummaryComplete\]false/);
    assert.equal(fs.existsSync(path.join(root, "eval-summary.md")), false);
});

test("invalid Prepare cannot reuse a stale selected directory", (t) => {
    const root = setup(t);
    const selected = path.join(root, "selected");
    fs.mkdirSync(selected);
    fs.writeFileSync(path.join(selected, "keep.txt"), "keep me");
    const child = runCli("build-eval-summary.ts", ["--results-root", path.join(root, "download"), "--selected-root", selected],
        { TF_BUILD: "true", EVAL_EXPECTED_MATRIX: "{}" }, root);
    assert.equal(child.status, 1);
    assert.match(child.stderr, /must be empty/);
    assert.match(child.stdout, /##vso\[task.setvariable variable=EvalSummaryComplete\]false/);
    assert.deepEqual(fs.readdirSync(selected), ["keep.txt"]);
    assert.equal(fs.readFileSync(path.join(selected, "keep.txt"), "utf8"), "keep me");
});

const timelineEnv = { SYSTEM_COLLECTIONURI: "https://dev.azure.com/azure-sdk/", SYSTEM_TEAMPROJECTID: "00000000-0000-4000-8000-000000000001",
    BUILD_BUILDID: "1001", SYSTEM_ACCESSTOKEN: "fixture-token", EVAL_EXPECTED_MATRIX: '{"a":{"shardName":"a"}}' };
const timelineRecord = { type: "Job", identifier: "Eval.RunShard.a", attempt: 1, state: "completed", result: "failed" };

test("timeline uses matrix identifiers, highest attempts and completed state, rejecting ambiguous attempts", () => {
    const matrix = { a: { shardName: "a" } }, records = [timelineRecord, { ...timelineRecord, attempt: 2 }];
    assert.deepEqual(latestShardAttempts({ records }, matrix).attempts.a, { attempt: 2, complete: true });
    records[1].result = "canceled";
    assert.equal(latestShardAttempts({ records }, matrix).attempts.a.complete, false);
    assert.throws(() => latestShardAttempts({ records: [] }, matrix));
    assert.throws(() => latestShardAttempts({ records }, {}));
    assert.throws(() => latestShardAttempts({ records: [timelineRecord, { ...timelineRecord, result: "canceled" }] }, matrix), /Ambiguous/);
});

test("newer failed attempt with no artifact cannot adopt an older complete artifact", t => {
    const root = setup(t); artifact(root, "a", 1);
    for (const [index, jobAttempts] of [undefined, null,
        { schemaVersion: 1, valid: true, attempts: { a: { attempt: 2, complete: true } } },
        { schemaVersion: 1, valid: true, attempts: { a: { attempt: 1, complete: false } } },
        { schemaVersion: 1, valid: false, attempts: {} }].entries()) {
        const selected = selectSummaryResults({ resultsRoot: path.join(root, "download"), selectedRoot: path.join(root, `selection-${index}`), expectedShards: ["a"], jobAttempts });
        assert.equal(selected.complete, false); assert.match(selected.status[0].reason, /timeline/);
        const matchingAttempt = jobAttempts?.valid === true && jobAttempts.attempts.a?.attempt === 1;
        assert.equal(selected.shardInput.attempts.length, matchingAttempt ? 1 : 0);
        assert.equal(fs.existsSync(path.join(root, `selection-${index}`, "eval-result-a-1", "junit", "0.junit.xml")), matchingAttempt);
    }
});

test("missing latest results contribute no older JUnit to Markdown, counts or selected test files", t => {
    const root = setup(t); artifact(root, "a", 1);
    const evidence = { schemaVersion: 1, valid: true, attempts: { a: { attempt: 2, complete: true } } };
    fs.writeFileSync(path.join(root, "job-attempts.json"), JSON.stringify(evidence));
    const { child, markdown, result, index } = runSummary(root, ["a"]);
    assert.equal(child.status, 1);
    assert.match(child.stdout, /EvalSummaryComplete\]false/);
    assert.equal(result.complete, false); assert.equal(index.complete, false);
    assert.equal(result.shards[0].attempt, 2); assert.match(result.shards[0].reason, /no matching artifact/);
    assert.deepEqual(result.totals, { scenarios: 0, failed: 0, skipped: 0 });
    assert.deepEqual(index.attempts, []);
    assert.deepEqual(fs.readdirSync(path.join(root, "selected")), []);
    assert.doesNotMatch(markdown, /source-a-1|results — PASSED/);
    assert.match(markdown, /results — INCOMPLETE/);
});

test("only the timeline-matching attempt is selected, even when newer unverified artifacts exist", t => {
    const root = setup(t); artifact(root, "a", 1); artifact(root, "a", 2, { failed: true }); artifact(root, "a", 3);
    fs.writeFileSync(path.join(root, "job-attempts.json"), JSON.stringify({ schemaVersion: 1, valid: true,
        attempts: { a: { attempt: 2, complete: true } } }));
    const { child, markdown, result, index } = runSummary(root, ["a"]);
    assert.equal(child.status, 0, child.stderr);
    assert.equal(result.complete, true); assert.equal(index.complete, true);
    assert.equal(result.shards[0].attempt, 2);
    assert.deepEqual(result.totals, { scenarios: 1, failed: 1, skipped: 0 });
    assert.deepEqual(fs.readdirSync(path.join(root, "selected")), ["eval-result-a-2"]);
    assert.deepEqual(index.attempts, [{ shard: "a", attempt: 2, directory: "eval-result-a-2" }]);
    assert.match(markdown, /results — FAILED/);
    assert.doesNotMatch(markdown, /source-a-1|source-a-3/);
});

test("timeline reads retry transient failures only, with a fixed three-attempt budget", async () => {
    const waits = []; let calls = 0;
    const result = await readShardAttempts(timelineEnv, { wait: async value => waits.push(value), fetchImpl: async (url, options) => {
        calls++; assert.equal(url.href, "https://dev.azure.com/azure-sdk/00000000-0000-4000-8000-000000000001/_apis/build/builds/1001/timeline?api-version=7.1");
        assert.equal(options.headers.authorization, "Bearer fixture-token"); assert.equal(options.redirect, "error");
        if (calls === 1) throw new Error("Lost response");
        if (calls === 2) return new Response("", { status: 503 });
        return Response.json({ records: [timelineRecord] });
    } });
    assert.equal(result.valid, true); assert.equal(calls, 3); assert.deepEqual(waits, [1000, 2000]);
    waits.length = 0; calls = 0;
    await readShardAttempts(timelineEnv, { wait: async value => waits.push(value), fetchImpl: async () => ++calls === 1 ?
        new Response("", { status: 429, headers: { "retry-after": "5" } }) : Response.json({ records: [timelineRecord] }) });
    assert.equal(calls, 2); assert.deepEqual(waits, [5000]);
    calls = 0;
    await assert.rejects(readShardAttempts(timelineEnv, { wait: async () => {}, fetchImpl: async () => { calls++; throw new Error("Offline"); } }));
    assert.equal(calls, 3);
    for (const status of [302, 401, 403]) {
        calls = 0;
        await assert.rejects(readShardAttempts(timelineEnv, { wait: () => assert.fail("Permanent failures do not retry"),
            fetchImpl: async () => { calls++; return new Response("", { status }); } }));
        assert.equal(calls, 1);
    }
});

test("invalid timeline configuration cannot send the build token to another endpoint", async () => {
    for (const collection of ["https://dev.azure.com:444/azure-sdk/", "https://dev.azure.com.attacker.example/azure-sdk/",
        "http://dev.azure.com/azure-sdk/", "https://user:password@dev.azure.com/azure-sdk/", "https://dev.azure.com/azure-sdk/?extra=1"]) {
        await assert.rejects(readShardAttempts({ ...timelineEnv, SYSTEM_COLLECTIONURI: collection }, { fetchImpl: () => assert.fail("Invalid endpoint reached fetch") }));
    }
});