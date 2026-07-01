// Agentic-workflow Copilot CLI extension.
//
// Self-contained, agent-first driver for a phased research -> plan -> implement workflow.
// One joinSession() registers a custom sub-agent per phase (phase prompt + access to all tools). The
// workflow never switches models: every phase runs on the user's configured Copilot CLI session
// model (claude-opus-4.8 is the recommended default). State lives entirely in a
// per-run directory on disk (<cwd>/.aw/<slug>/) so the workflow is inspectable, resumable, and
// survives an extension reload. The agent self-reports each phase with a sentinel line:
//   PHASE_RESULT: pass | fail | needs_input  (+ optional reason)
// which the dispatch loop and auto-runner read instead of any deterministic validator.
//
// Protocol hygiene: .mjs only; diagnostics go to stderr; user-facing messages go to session.log.

import { joinSession } from "@github/copilot-sdk/extension";
import { fileURLToPath } from "node:url";
import * as path from "node:path";
import * as fs from "node:fs";
import { createHash } from "node:crypto";
import { execSync } from "node:child_process";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PROMPTS_DIR = path.join(HERE, "prompts");
const PHASE_TIMEOUT_MS = 30 * 60 * 1000; // generous wait; does not abort in-flight agent work
const MAX_RETRIES = 2;

// --- Phase registry: the per-phase config (tools + prompt). -------------------------------------
// The workflow does not switch models; every phase runs on the user's session model. Tool access +
// prompt are bound into customAgents at join time. `artifact` is the sentinel file/dir that marks
// the phase complete. `simple` phases form the abbreviated flow used by `/rpi-start <task> simple`.
const ALL_TOOLS = null;

const PHASES = [
    { id: "research", agent: "rpi-research", template: "01-research.md", tools: ALL_TOOLS, artifact: "specs/architecture.md", simple: true },
    { id: "assumptions", agent: "rpi-assumptions", template: "02-assumptions.md", tools: ALL_TOOLS, artifact: "assumptions.md", simple: true },
    { id: "classify", agent: "rpi-classify", template: "03-classify.md", tools: ALL_TOOLS, artifact: "subitems.json", simple: false },
    { id: "research-item", agent: "rpi-research-item", template: "04-research-item.md", tools: ALL_TOOLS, artifact: "research", simple: false },
    { id: "plan", agent: "rpi-plan", template: "05-plan.md", tools: ALL_TOOLS, artifact: "plan.md", simple: true },
    { id: "implement", agent: "rpi-implement", template: "06-implement.md", tools: ALL_TOOLS, artifact: "execution-log.md", simple: true },
];


// --- Module-scoped run state (in memory; the run dir on disk is the source of truth). ------------
let session;
let activeTask = "";
let activeRunDir = "";
let activeSimple = false;
let autoJudge = false;
let pauseRequested = false;

const log = (msg) => session?.log(`agentic-workflow: ${msg}`).catch(() => {});
const diag = (msg) => process.stderr.write(`[agentic-workflow] ${msg}\n`);

// --- Run-dir + phase helpers ---------------------------------------------------------------------
function slugify(task) {
    const s = task.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40);
    return s || "run";
}
export { slugify };

// Deterministic run id: human-readable slug + short content hash. The hash disambiguates tasks that
// share a 40-char slug prefix (which would otherwise collide onto the same run dir) and is stable
// for a given task, so `/rpi-start <same task>` and `/rpi-resume` resolve to the exact same dir.
function runId(task) {
    const h = createHash("sha1").update(task, "utf8").digest("hex").slice(0, 8);
    return `${slugify(task)}-${h}`;
}
export { runId };

function readTemplate(name) {
    return fs.readFileSync(path.join(PROMPTS_DIR, name), "utf8");
}

// --- On-disk run state (state.json): the durable record of what each phase reported + run meta. ---
// The artifact files remain the primary output; state.json records the PHASE_RESULT sentinel so a
// phase counts as complete only when it actually reported `pass` (not merely because it left a
// partial file behind), and carries the metadata needed to resume after an extension reload.
function stateFile(runDir) {
    return path.join(runDir, "state.json");
}

function readState(runDir) {
    try {
        return JSON.parse(fs.readFileSync(stateFile(runDir), "utf8"));
    } catch {
        return null;
    }
}

function writeState(runDir, state) {
    try {
        fs.writeFileSync(stateFile(runDir), JSON.stringify(state, null, 2) + "\n");
    } catch (e) {
        diag(`could not write state.json: ${e?.message ?? e}`);
    }
}

function recordPhaseResult(runDir, phaseId, result, reason) {
    const state = readState(runDir) || {};
    state.phases = state.phases || {};
    state.phases[phaseId] = { result, reason: reason || "", at: new Date().toISOString() };
    writeState(runDir, state);
}
export { recordPhaseResult, writeState };

// research-item is complete only when there is a note for EVERY sub-item in subitems.json, not just
// when the research/ dir holds some file — a phase that wrote 1 of N notes then failed is not done.
function researchNotesComplete(runDir) {
    let ids = [];
    try {
        const sub = JSON.parse(fs.readFileSync(path.join(runDir, "subitems.json"), "utf8"));
        ids = (sub.items || []).map((it) => it.id).filter(Boolean);
    } catch {
        return false; // cannot verify coverage without a readable subitems.json
    }
    if (ids.length === 0) return false;
    return ids.every((id) => fs.existsSync(path.join(runDir, "research", `${id}.md`)));
}
export { researchNotesComplete };

function phaseOrder(simple) {
    return simple ? PHASES.filter((p) => p.simple) : PHASES;
}

// A phase is complete only when it reported `pass` AND its artifact exists (with per-phase coverage
// checks). Requiring the recorded pass stops a failed phase that left a partial artifact from being
// silently skipped by the auto-runner.
function phaseComplete(runDir, phase) {
    const state = readState(runDir);
    if (state?.phases?.[phase.id]?.result !== "pass") return false;
    const target = path.join(runDir, phase.artifact);
    if (!fs.existsSync(target)) return false;
    if (phase.id === "research-item") return researchNotesComplete(runDir);
    try {
        const st = fs.statSync(target);
        if (st.isDirectory()) return fs.readdirSync(target).length > 0;
    } catch {
        return false;
    }
    return true;
}

function nextPhase(runDir, simple) {
    return phaseOrder(simple).find((p) => !phaseComplete(runDir, p)) || null;
}
export { nextPhase, phaseComplete };

function phaseById(id) {
    return PHASES.find((p) => p.id === id) || null;
}

// Parse the agent's self-reported sentinel line. Returns { result, reason }.
export function parsePhaseResult(text) {
    const m = (text ?? "").match(/PHASE_RESULT:\s*(pass|fail|needs_input)\b[^\S\r\n]*[—:-]?[^\S\r\n]*(.*)/i);
    const result = (m?.[1] ?? "fail").toLowerCase();
    const reason = (m?.[2] ?? (m ? "" : "no PHASE_RESULT sentinel found")).trim();
    return { result, reason };
}

// --- Core dispatch: select agent, send phase prompt, parse PHASE_RESULT. -------------------------
async function dispatch(phase, { priorErrors = "" } = {}) {
    if (!activeRunDir) {
        await log("no active run. Start one with /rpi-start <task>.");
        return { result: "fail", reason: "no active run", text: "" };
    }
    // Automatic context check before the expensive implement phase.
    if (phase.id === "implement") {
        try {
            await session.rpc.commands.enqueue({ command: "/compact" });
        } catch (e) {
            diag(`pre-implement compact skipped: ${e?.message ?? e}`);
        }
    }
    await session.rpc.agent.select({ name: phase.agent });

    const priorBlock = priorErrors ? `## Prior feedback to address\n${priorErrors}\n` : "";
    const prompt = readTemplate(phase.template)
        .replaceAll("{{task}}", activeTask)
        .replaceAll("{{runDir}}", activeRunDir)
        .replaceAll("{{priorErrors}}", priorBlock);

    await log(`running ${phase.id}…`);
    const ev = await session.sendAndWait({ prompt }, PHASE_TIMEOUT_MS);
    const text = ev?.data?.content ?? "";
    const { result, reason } = parsePhaseResult(text);
    if (activeRunDir) recordPhaseResult(activeRunDir, phase.id, result, reason);
    diag(`phase ${phase.id} -> ${result}${reason ? ` (${reason})` : ""}`);
    return { result, reason, text };
}

// --- Human interaction (elicitation), gated on capability + unattended. --------------------------
function uiAvailable() {
    return Boolean(session?.capabilities?.ui?.elicitation);
}

async function askHuman(question, unattended) {
    if (unattended || !uiAvailable()) {
        return "No human is available (unattended run). Proceed with your most reasonable assumption and document it explicitly.";
    }
    const answer = await session.ui.input(`Needs input: ${question}`);
    return answer; // null if the user cancels
}

async function resolveFailure(phase, reason, unattended) {
    if (unattended || !uiAvailable()) return "abort";
    const choice = await session.ui.select(`Phase ${phase.id} failed: ${reason || "unknown"}. What now?`, ["retry", "skip", "abort"]);
    return choice ?? "abort";
}

// --- Auto-runner: walk phases from -> to, branching on PHASE_RESULT. ------------------------------
async function autoRun({ from, to, unattended = false, pauseAt } = {}) {
    if (!activeRunDir) {
        await log("no active run. Start one with /rpi-auto <task>.");
        return;
    }
    pauseRequested = false;
    const order = phaseOrder(activeSimple);
    let startIdx = from ? order.findIndex((p) => p.id === from) : -1;
    if (startIdx < 0) {
        const next = nextPhase(activeRunDir, activeSimple);
        startIdx = next ? order.findIndex((p) => p.id === next.id) : order.length;
    }
    const toIdx = to ? order.findIndex((p) => p.id === to) : order.length - 1;
    if (toIdx < 0) {
        await log(`unknown 'to' phase: ${to}`);
        return;
    }

    for (let i = startIdx; i <= toIdx; i++) {
        if (pauseRequested) {
            await log(`paused before ${order[i].id}.`);
            return;
        }
        const phase = order[i];
        if (phaseComplete(activeRunDir, phase)) {
            diag(`skipping completed phase ${phase.id}`);
            continue;
        }

        let priorErrors = "";
        let retries = 0;
        let passed = false;
        // Snapshot artifacts before the phase runs so we can rubber-duck exactly what it generates.
        const before = new Set(walkArtifacts(activeRunDir));
        // Re-dispatch this phase until it passes, is skipped, or the run aborts.
        for (;;) {
            const res = await dispatch(phase, { priorErrors });
            if (res.result === "pass") {
                passed = true;
                break;
            }

            if (res.result === "needs_input") {
                const answer = await askHuman(res.reason || `${phase.id} needs a decision`, unattended);
                if (answer === null) {
                    await log(`stopped: ${phase.id} needs input and the request was cancelled.`);
                    return;
                }
                priorErrors = answer;
                retries = 0;
                continue;
            }

            // fail
            retries += 1;
            if (retries > MAX_RETRIES) {
                const decision = await resolveFailure(phase, res.reason, unattended);
                if (decision === "abort") {
                    await log(`aborted at ${phase.id} after ${MAX_RETRIES} retries: ${res.reason || "failed"}.`);
                    return;
                }
                if (decision === "skip") {
                    await log(`skipping ${phase.id} (manual override).`);
                    break;
                }
                retries = 0; // retry
            }
            priorErrors = res.reason || "The previous attempt did not pass. Fix the issues and try again.";
        }

        // Auto-judge: when enabled, rubber-duck every artifact this phase generated (append-only).
        if (passed && autoJudge) await autoJudgePhase(phase, before);

        if (pauseAt && phase.id === pauseAt) {
            await log(`paused at breakpoint ${pauseAt}.`);
            return;
        }
    }
    await log(`auto-run reached ${order[toIdx].id}.`);
}

// --- Tiny arg parsing (key:value pairs + free text). ---------------------------------------------
function parseKv(argstr) {
    const kv = {};
    const rest = [];
    for (const tok of (argstr ?? "").trim().split(/\s+/).filter(Boolean)) {
        const m = tok.match(/^([a-z-]+):(.*)$/i);
        if (m) kv[m[1].toLowerCase()] = m[2];
        else rest.push(tok);
    }
    return { kv, rest };
}

function isTruthy(v) {
    return v === "" || /^(true|1|yes|on)$/i.test(v ?? "");
}

// Extract the task string and simple-flow flag from a command's args. Shared by /rpi-start and
// /rpi-auto so both entry points parse tasks identically. `simple` is set by forceSimple, a
// `simple:` kv pair, or a bare `simple` token in the free text; those tokens are stripped from the
// task. Returns the raw kv map too so callers can read from:/to:/unattended:/pause-at:.
function parseTaskArgs(argstr, forceSimple = false) {
    const { kv, rest } = parseKv(argstr);
    const simple = forceSimple || isTruthy(kv.simple) || rest.some((t) => /^(--?)?simple$/i.test(t));
    const task = rest.filter((t) => !/^(--?)?simple$/i.test(t)).join(" ").trim() || kv.task || "";
    return { task, simple, kv };
}
export { parseKv, isTruthy, parseTaskArgs };

function isDiffJudgeTarget(argstr) {
    return /^(--?)?(diff|git[- ]?diff|changes|code[- ]?changes)$/i.test((argstr ?? "").trim());
}

function diffCritiqueRel(date = new Date()) {
    return `critiques/git-diff-${date.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z")}.md`;
}

function buildDiffJudgeInstruction({ cwd, runDir, critiqueRel, task }) {
    const critiqueAbs = path.join(runDir, critiqueRel);
    return (
        `Review the current repository code changes for the agentic-workflow run "${task}". ` +
        `Work in \`${cwd}\`. Run \`git status --short\`, \`git diff HEAD --stat\`, and ` +
        `\`git diff HEAD -- . ':(exclude).aw'\` to inspect staged and unstaged implementation changes. ` +
        `If useful, compare the changes with \`${path.join(runDir, "plan.md")}\` and ` +
        `\`${path.join(runDir, "execution-log.md")}\`. Focus on correctness bugs, regressions, ` +
        `missed requirements, unsafe behavior, and meaningful test gaps. Do not change code. ` +
        `Write the critique as markdown to \`${critiqueAbs}\`, creating parent directories as needed.`
    );
}
export { isDiffJudgeTarget, diffCritiqueRel, buildDiffJudgeInstruction };

// --- Commands ------------------------------------------------------------------------------------
// Ensure the target repo ignores the on-disk run state so `.aw/` artifacts never leak into commits.
function ensureGitignore() {
    try {
        const gi = path.join(process.cwd(), ".gitignore");
        let content = "";
        try {
            content = fs.readFileSync(gi, "utf8");
        } catch {
            content = "";
        }
        const already = content.split(/\r?\n/).some((l) => l.trim().replace(/\/+$/, "") === ".aw");
        if (already) return;
        const prefix = content && !content.endsWith("\n") ? "\n" : "";
        fs.appendFileSync(gi, `${prefix}.aw/\n`);
        diag("added .aw/ to .gitignore");
    } catch (e) {
        diag(`could not update .gitignore: ${e?.message ?? e}`);
    }
}

async function cmdStart(argstr, { forceSimple = false } = {}) {
    const { task, simple } = parseTaskArgs(argstr, forceSimple);
    if (!task) {
        await log("provide a task, e.g. /rpi-start Add CSV export, or /rpi-start Fix bug simple");
        return;
    }
    initRun(task, simple);
    await log(`run dir: ${path.relative(process.cwd(), activeRunDir) || activeRunDir}${simple ? " (simple flow)" : ""}`);
    const next = nextPhase(activeRunDir, activeSimple);
    if (!next) {
        await log("all phases already complete for this task. Use /rpi-status or /rpi-redo <phase>.");
        return;
    }
    await dispatch(next);
}

// Initialize (or re-attach to) a run for `task`: set module state, create the run dir, protect it
// via .gitignore, persist task/flow metadata (preserving prior phase results for an existing run
// dir), and restore the auto-judge toggle. Shared by /rpi-start and /rpi-auto.
function initRun(task, simple) {
    activeTask = task;
    activeSimple = simple;
    activeRunDir = path.join(process.cwd(), ".aw", runId(task));
    fs.mkdirSync(activeRunDir, { recursive: true });
    ensureGitignore();
    fs.writeFileSync(path.join(activeRunDir, "task.txt"), task + "\n");
    // Persist/refresh run metadata so the run can be resumed after a reload; preserve prior phase
    // results if this task's run dir already exists.
    const state = readState(activeRunDir) || {};
    state.task = task;
    state.simple = simple;
    writeState(activeRunDir, state);
    autoJudge = !!state.autoJudge;
}
export { initRun };

// List resumable runs under <cwd>/.aw/, newest first, from their persisted state.json.
function listRuns() {
    const base = path.join(process.cwd(), ".aw");
    let names = [];
    try {
        names = fs.readdirSync(base, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
    } catch {
        return [];
    }
    return names
        .map((name) => {
            const dir = path.join(base, name);
            const st = readState(dir) || {};
            let mtime = 0;
            try {
                mtime = fs.statSync(dir).mtimeMs;
            } catch {
                mtime = 0;
            }
            return { name, dir, task: st.task || name, simple: !!st.simple, mtime };
        })
        .sort((a, b) => b.mtime - a.mtime);
}
export { listRuns };

// Restore module state (and the auto-judge toggle) from a persisted run so the workflow can
// continue after an extension reload without re-typing the exact task string.
async function cmdResume(argstr) {
    const query = (argstr ?? "").trim();
    const runs = listRuns();
    if (runs.length === 0) {
        await log("no runs found under .aw/. Start one with /rpi-start <task>.");
        return;
    }
    let run;
    if (query) {
        const q = query.toLowerCase();
        run = runs.find((r) => r.name === query) || runs.find((r) => r.task.toLowerCase().includes(q));
        if (!run) {
            await log(`no run matches "${query}". Available:\n${runs.map((r) => `  ${r.name} — ${r.task}`).join("\n")}`);
            return;
        }
    } else if (runs.length === 1) {
        run = runs[0];
    } else {
        await log(`multiple runs found; pick one with /rpi-resume <name-or-text>:\n${runs.map((r) => `  ${r.name} — ${r.task}`).join("\n")}`);
        return;
    }
    activeTask = run.task;
    activeSimple = run.simple;
    activeRunDir = run.dir;
    autoJudge = !!readState(run.dir)?.autoJudge;
    const next = nextPhase(activeRunDir, activeSimple);
    await log(
        `resumed "${activeTask}" (${path.relative(process.cwd(), activeRunDir) || activeRunDir})` +
            `${next ? `; next phase: ${next.id}. Use /rpi-continue or /rpi-auto.` : "; all phases complete."}`,
    );
}

async function cmdPhase(id, argstr) {
    const phase = phaseById(id);
    if (!phase) {
        await log(`unknown phase: ${id}`);
        return;
    }
    const priorErrors = (argstr ?? "").trim();
    await dispatch(phase, { priorErrors });
}

async function cmdContinue(argstr) {
    const n = Math.max(1, parseInt((argstr ?? "").trim(), 10) || 1);
    for (let k = 0; k < n; k++) {
        const next = nextPhase(activeRunDir, activeSimple);
        if (!next) {
            await log("no remaining phases.");
            return;
        }
        const res = await dispatch(next);
        if (res.result !== "pass") {
            await log(`stopped at ${next.id} (${res.result}${res.reason ? `: ${res.reason}` : ""}).`);
            return;
        }
    }
}

// Auto-run entry point. With a task, cold-start a run (init dir + state) then auto-run to
// completion; with no task and an active run, auto-run the active run from the next incomplete
// phase (the former /rpi-run behavior); with neither, print guidance. Honors from:/to:/unattended:/
// pause-at: kv args.
async function cmdAuto(argstr, { forceSimple = false } = {}) {
    const { task, simple, kv } = parseTaskArgs(argstr, forceSimple);
    if (task) {
        initRun(task, simple);
        await log(`run dir: ${path.relative(process.cwd(), activeRunDir) || activeRunDir}${simple ? " (simple flow)" : ""} (auto)`);
    } else if (!activeRunDir) {
        await log("no active run. Provide a task: /rpi-auto <task> (or /rpi-auto-simple <task>), or /rpi-resume first.");
        return;
    }
    await autoRun({
        from: kv.from,
        to: kv.to,
        unattended: isTruthy(kv.unattended),
        pauseAt: kv["pause-at"] || kv.pauseat,
    });
}

async function cmdPause() {
    pauseRequested = true;
    await log("pause requested; the auto-runner will stop at the next phase boundary.");
}

// Rubber-duck one artifact: enqueue the native /rubber-duck command, instructing it to review the
// artifact and append its critique to that file. Append-only; the file's existing content is not
// otherwise changed. Non-markdown artifacts (e.g. JSON) are skipped so their structure stays valid.
async function judgeArtifact(artifactRel) {
    if (!activeRunDir) {
        await log("no active run. Start one with /rpi-start <task>.");
        return;
    }
    if (!/\.md$/i.test(artifactRel)) {
        await log(`skipping /rpi-judge for ${artifactRel} (not a markdown artifact; append-critique would corrupt it).`);
        return;
    }
    const abs = path.join(activeRunDir, artifactRel);
    const instr =
        `Review the workflow artifact at \`${abs}\`. Read the whole file, then append your critique ` +
        `to the END of that same file as a new section titled ` +
        `"## Rubber-duck critique (${new Date().toISOString()})". Make no other changes to the file.`;
    try {
        await session.rpc.commands.enqueue({ command: `/rubber-duck ${instr}` });
        await log(`queued /rubber-duck critique for ${artifactRel} (appended to the artifact).`);
    } catch (e) {
        await log(`rubber-duck failed for ${artifactRel}: ${e?.message ?? e}`);
    }
}

// After a phase passes under auto-judge, rubber-duck every markdown artifact the phase just
// generated. `before` is the artifact set snapshotted before the phase ran, so only new files are
// critiqued (existing artifacts from earlier phases are left alone).
async function autoJudgePhase(phase, before) {
    const generated = walkArtifacts(activeRunDir)
        .filter((rel) => rel !== "task.txt" && rel !== "state.json")
        .filter((rel) => /\.md$/i.test(rel))
        .filter((rel) => !before.has(rel));
    if (generated.length === 0) {
        diag(`auto-judge: no new markdown artifacts to critique for ${phase.id}`);
    } else {
        for (const rel of generated) {
            await log(`auto-judging ${rel} (from ${phase.id})…`);
            await judgeArtifact(rel);
        }
    }
    if (phase.id === "implement") {
        await log("auto-judging implementation git diff…");
        await judgeDiff();
    }
}

// On-demand critique. `/rpi-judge <artifact>` rubber-ducks that artifact and appends the critique to
// it. `/rpi-judge diff` rubber-ducks the current git diff and writes a critique artifact. With no
// argument, enqueue a plain /rubber-duck to critique the current work inline.
async function cmdJudge(argstr) {
    const artifact = (argstr ?? "").trim();
    if (isDiffJudgeTarget(artifact)) {
        await judgeDiff();
        return;
    }
    if (artifact) {
        await judgeArtifact(artifact);
        return;
    }
    try {
        await session.rpc.commands.enqueue({ command: "/rubber-duck" });
        await log("queued /rubber-duck for an independent critique of the current work.");
    } catch (e) {
        await log(`rubber-duck failed: ${e?.message ?? e}`);
    }
}

async function judgeDiff() {
    if (!activeRunDir) {
        await log("no active run. Start one with /rpi-start <task>.");
        return;
    }
    const critiqueRel = diffCritiqueRel();
    const instr = buildDiffJudgeInstruction({
        cwd: process.cwd(),
        runDir: activeRunDir,
        critiqueRel,
        task: activeTask,
    });
    try {
        fs.mkdirSync(path.join(activeRunDir, path.dirname(critiqueRel)), { recursive: true });
        await session.rpc.commands.enqueue({ command: `/rubber-duck ${instr}` });
        await log(`queued /rubber-duck critique for git diff (${critiqueRel}).`);
    } catch (e) {
        await log(`rubber-duck failed for git diff: ${e?.message ?? e}`);
    }
}

// Toggle auto-judge for the active run. `/rpi-autojudge` flips it; `on`/`off` set it explicitly.
// When on, the auto-runner rubber-ducks every markdown artifact each phase generates after it passes.
async function cmdAutoJudge(argstr) {
    const v = (argstr ?? "").trim().toLowerCase();
    if (v === "on" || v === "off") autoJudge = v === "on";
    else autoJudge = !autoJudge;
    if (activeRunDir) {
        const state = readState(activeRunDir) || {};
        state.autoJudge = autoJudge;
        writeState(activeRunDir, state);
    }
    await log(`auto-judge is now ${autoJudge ? "ON" : "OFF"}${autoJudge ? " — each phase's new markdown artifacts get a /rubber-duck critique appended after it passes, and implement also gets a git-diff critique." : "."}`);
}

async function cmdRedo(argstr) {
    const { rest } = parseKv(argstr);
    const id = rest.shift();
    const feedback = rest.join(" ").trim();
    if (!id) {
        await log("usage: /rpi-redo <phase> <feedback>");
        return;
    }
    await cmdPhase(id, feedback);
}

function walkArtifacts(dir, base = dir, out = []) {
    let entries = [];
    try {
        entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
        return out;
    }
    for (const e of entries) {
        const full = path.join(dir, e.name);
        if (e.isDirectory()) walkArtifacts(full, base, out);
        else out.push(path.relative(base, full));
    }
    return out;
}

async function cmdStatus() {
    if (!activeRunDir) {
        await log("no active run. Start one with /rpi-start <task>.");
        return;
    }
    const order = phaseOrder(activeSimple);
    const phaseLines = order.map((p) => `  ${phaseComplete(activeRunDir, p) ? "[x]" : "[ ]"} ${p.id}`).join("\n");
    const artifacts = walkArtifacts(activeRunDir).filter((f) => f !== "task.txt" && f !== "state.json").sort();
    let diffStat = "";
    try {
        diffStat = execSync("git diff --stat", { cwd: process.cwd(), encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
    } catch {
        diffStat = "(not a git repo or git unavailable)";
    }
    await log(
        `status for "${activeTask}"\nphases:\n${phaseLines}\nartifacts (${artifacts.length}):\n  ${artifacts.join("\n  ") || "(none yet)"}\ngit diff --stat:\n${diffStat || "(no changes)"}`,
    );
}

async function cmdCompact() {
    try {
        await session.rpc.commands.enqueue({ command: "/compact" });
        await log("queued /compact to reclaim context.");
    } catch (e) {
        await log(`compact failed: ${e?.message ?? e}`);
    }
}

// --- Register the session: per-phase sub-agents + commands. --------------------------------------
// The agent's pinned identity = role + tool access. The full, parameterized phase instructions
// (the durable IP in prompts/) are delivered per-dispatch via sendAndWait, since the templates
// depend on the task/run dir which are unknown at join time. Keeping a short role prompt here
// avoids injecting unfilled {{task}}/{{runDir}} placeholders into the agent context.
const customAgents = PHASES.map((p) => ({
    name: p.agent,
    displayName: p.agent,
    description: `Agentic-workflow ${p.id} phase`,
    tools: p.tools, // null => all tools
    prompt:
        `You are the **${p.id}** phase of an automated research → plan → implement workflow. ` +
        `Follow the detailed phase instructions delivered in each message exactly, persist artifacts ` +
        `under the run directory you are given, and end your turn ` +
        `with the single sentinel line \`PHASE_RESULT: pass | fail | needs_input\` (plus an optional reason).`,
}));

const cmd = (name, description, handler) => ({ name, description, handler: (ctx) => Promise.resolve(handler(ctx)).catch((e) => diag(`/${name} error: ${e?.stack ?? e}`)) });

// The full join configuration, built once so it can be inspected by tests (the six agents and the
// command surface) without standing up a live session host.
export const joinConfig = {
    customAgents,
    infiniteSessions: { enabled: true },
    commands: [
        cmd("rpi-start", "Start a full workflow run on a task.", (c) => cmdStart(c.args)),
        cmd("rpi-start-simple", "Start a short-flow run (research → assumptions → plan → implement).", (c) => cmdStart(c.args, { forceSimple: true })),
        cmd("rpi-resume", "Resume a run after a reload: /rpi-resume [name-or-text] (rehydrates from .aw/).", (c) => cmdResume(c.args)),
        cmd("rpi-auto", "Start (if given a task) and auto-run to completion: /rpi-auto <task> [from:<p>] [to:<p>] [unattended:true] [pause-at:<p>].", (c) => cmdAuto(c.args)),
        cmd("rpi-auto-simple", "Same as /rpi-auto but the short flow (research → assumptions → plan → implement).", (c) => cmdAuto(c.args, { forceSimple: true })),
        cmd("rpi-continue", "Run the next phase (or next N).", (c) => cmdContinue(c.args)),
        cmd("rpi-pause", "Stop the auto-runner at the next phase boundary.", () => cmdPause()),
        cmd("rpi-research", "Run the research phase.", (c) => cmdPhase("research", c.args)),
        cmd("rpi-assumptions", "Run the assumptions phase.", (c) => cmdPhase("assumptions", c.args)),
        cmd("rpi-classify", "Run the classify phase.", (c) => cmdPhase("classify", c.args)),
        cmd("rpi-research-item", "Run the per-sub-item research phase.", (c) => cmdPhase("research-item", c.args)),
        cmd("rpi-plan", "Run the plan phase.", (c) => cmdPhase("plan", c.args)),
        cmd("rpi-implement", "Run the implement phase.", (c) => cmdPhase("implement", c.args)),
        cmd("rpi-judge", "Rubber-duck an artifact or git diff: /rpi-judge <artifact|diff>. No arg critiques current work inline.", (c) => cmdJudge(c.args)),
        cmd("rpi-autojudge", "Toggle auto-judge: rubber-duck phase artifacts and the post-implement git diff. /rpi-autojudge [on|off].", (c) => cmdAutoJudge(c.args)),
        cmd("rpi-redo", "Re-run a phase with steering notes: /rpi-redo <phase> <feedback>.", (c) => cmdRedo(c.args)),
        cmd("rpi-status", "Show phase state, artifacts, and git diff --stat.", () => cmdStatus()),
        cmd("rpi-compact", "Reclaim context by queuing /compact.", () => cmdCompact()),
    ],
};

// Guard so the pure helpers / config can be imported by tests without a live session host.
if (!process.env.AW_SKIP_JOIN) {
    session = await joinSession(joinConfig);
    diag(`loaded with ${customAgents.length} phase agents; run dir base ${path.join(process.cwd(), ".aw")}`);
}
