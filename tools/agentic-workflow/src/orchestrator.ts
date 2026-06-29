/**
 * orchestrator.ts — sequences phases 1->6 through the stable `Harness` seam, reading/writing
 * artifacts, validating each, retrying with a FRESH session on failure (the one enforced
 * guarantee), fanning out phase 4, and running the reflexive judge loop on validated artifacts.
 *
 * It never imports the SDK — only the `Harness` interface — so harness churn cannot reach here.
 */
import { spawnSync } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";
import { ensureRunDir, makeRunId, readIfExists } from "./artifacts.js";
import { parsePlanStages } from "./gates.js";
import type { Harness } from "./harness.js";
import { renderTemplate, type PromptName } from "./prompts.js";
import { ensureLog, logEvent } from "./session-options.js";
import { initState, loadState, PHASE_ORDER, saveState, setPhase } from "./state.js";
import type { RunOptions, RunState, Stage, SubItems, ValidationResult } from "./types.js";
import { validatePlan, validateSubitemsJson } from "./validate.js";

export interface RunResult {
    /** 0 done, 10 paused, 1 fail, 2 usage. */
    exitCode: number;
    runId: string;
    runDir: string;
    message: string;
}

/** Per-phase model defaults (overridable). Cheap where possible, strong where it matters. */
const PHASE_MODELS: Record<string, string | undefined> = {
    research: "claude-sonnet-4.5",
    assumptions: undefined,
    classify: "claude-haiku-4.5",
    "research-item": "claude-sonnet-4.5",
    plan: "claude-sonnet-4.5",
    implement: "claude-sonnet-4.5",
    critique: "claude-haiku-4.5",
};

export async function runWorkflow(opts: RunOptions, harness: Harness): Promise<RunResult> {
    const runId = opts.runId ?? makeRunId(opts.task);
    const { dir: runDir } = ensureRunDir(runId, { outRoot: opts.outRoot });
    const logPath = path.join(runDir, "execution-log.jsonl");
    ensureLog(logPath);

    const simple = !!opts.simple;
    const judge = opts.judge !== false;
    const maxRetries = opts.maxRetries ?? 2;
    const concurrency = opts.concurrency ?? 3;

    let state = loadState(runDir);
    if (!state) {
        state = initState(runDir, { runId, task: opts.task, simple, judge, judgeModel: opts.judgeModel });
    }

    const done = (phase: string) => state!.phases[phase] === "completed";
    const researchNote = simple
        ? "Research was skipped (--simple); read the task description and codebase directly."
        : "";

    /** Run one phase: render template -> harness.runPhase -> validate; retry FRESH on failure. */
    const runPhase = async (
        phase: string,
        template: PromptName,
        vars: Record<string, string | undefined>,
        validate: () => ValidationResult,
        readOnly: boolean,
        label = phase,
    ): Promise<boolean> => {
        setPhase(runDir, state!, phase, "in_progress");
        let priorErrors = "";
        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            const prompt = renderTemplate(template, { ...vars, priorErrors });
            await harness.runPhase({
                label: attempt === 0 ? label : `${label} (retry ${attempt})`,
                prompt,
                runDir,
                logPath,
                readOnly,
                model: resolveModel(phase, opts.judgeModel, opts),
            });
            const result = validate();
            if (result.ok) {
                setPhase(runDir, state!, phase, "completed");
                return true;
            }
            priorErrors =
                `\n## Your previous attempt failed validation. Fix exactly these and rewrite the artifact(s):\n` +
                result.errors.map((e) => `- ${e}`).join("\n") +
                `\n`;
            logEvent(logPath, { kind: "validation_failed", phase, attempt, errors: result.errors });
        }
        setPhase(runDir, state!, phase, "failed");
        return false;
    };

    const exists = (rel: string) => fs.existsSync(path.join(runDir, rel));
    const fail = (message: string): RunResult => ({ exitCode: 1, runId, runDir, message });
    const paused = (message: string): RunResult => ({ exitCode: 10, runId, runDir, message });

    // ---- Phase 1: research (skippable) ----
    if (!simple && !done("research")) {
        const ok = await runPhase(
            "research",
            "01-research",
            { task: opts.task },
            () =>
                exists("specs/architecture.md") && exists("specs/functional.md") && exists("manifest.json")
                    ? { ok: true, errors: [] }
                    : { ok: false, errors: ["expected specs/architecture.md, specs/functional.md, manifest.json"] },
            true,
        );
        if (!ok) return fail("research phase failed validation after retries");
        if (judge)
            await judgeArtifact(harness, runDir, logPath, "specs/architecture.md", opts, () => ({
                ok: true,
                errors: [],
            }));
    } else if (simple) {
        setPhase(runDir, state, "research", "completed");
    }

    // ---- Phase 2: assumptions ----
    if (!done("assumptions")) {
        const ok = await runPhase(
            "assumptions",
            "02-assumptions",
            { task: opts.task, researchNote },
            () =>
                exists("assumptions.md")
                    ? { ok: true, errors: [] }
                    : { ok: false, errors: ["assumptions.md not written"] },
            true,
        );
        if (!ok) return fail("assumptions phase failed validation after retries");
        if (judge)
            await judgeArtifact(harness, runDir, logPath, "assumptions.md", opts, () => ({ ok: true, errors: [] }));

        // Blocking-clarification stop (fires regardless of mode).
        const assumptions = readIfExists(path.join(runDir, "assumptions.md")) ?? "";
        if (/blocking:\s*true/i.test(assumptions)) {
            logEvent(logPath, { kind: "blocking_clarification", phase: "assumptions" });
            return paused("A blocking assumption needs human clarification before continuing. See assumptions.md.");
        }
    }

    // ---- Phase 3: classify (skippable -> synthesize single-item subitems.json) ----
    if (!done("classify")) {
        if (simple) {
            const synthesized: SubItems = {
                task: opts.task,
                classification: "feature",
                items: [
                    {
                        id: "main",
                        type: "feature",
                        title: opts.task.slice(0, 60),
                        description: opts.task,
                        dependsOn: [],
                        overlapRisk: "low",
                    },
                ],
            };
            fs.writeFileSync(path.join(runDir, "subitems.json"), JSON.stringify(synthesized, null, 2));
            setPhase(runDir, state, "classify", "completed");
        } else {
            const ok = await runPhase(
                "classify",
                "03-classify",
                { task: opts.task, researchNote },
                () => validateSubitemsJson(readIfExists(path.join(runDir, "subitems.json")) ?? ""),
                true,
            );
            if (!ok) return fail("classify phase failed validation after retries");
        }
    }

    const subitems = JSON.parse(readIfExists(path.join(runDir, "subitems.json")) ?? "{}") as SubItems;

    // ---- Phase 4: research-item fan-out (skippable) ----
    if (!simple && !done("research-item")) {
        setPhase(runDir, state, "research-item", "in_progress");
        const okAll = await fanOutResearch(harness, runDir, logPath, opts, subitems, concurrency, maxRetries);
        if (!okAll) return fail("one or more research-item phases failed");
        setPhase(runDir, state, "research-item", "completed");
    } else if (simple) {
        setPhase(runDir, state, "research-item", "completed");
    }

    // ---- Phase 5: plan ----
    if (!done("plan")) {
        const ok = await runPhase(
            "plan",
            "05-plan",
            { task: opts.task, researchNote },
            () => validatePlan(readIfExists(path.join(runDir, "plan.md")) ?? ""),
            true,
        );
        if (!ok) return fail("plan phase failed validation after retries");
        if (judge) {
            await judgeArtifact(harness, runDir, logPath, "plan.md", opts, () =>
                validatePlan(readIfExists(path.join(runDir, "plan.md")) ?? ""),
            );
        }
    }

    // ---- Phase 6: staged implement ----
    if (!done("implement")) {
        setPhase(runDir, state, "implement", "in_progress");
        let stages: Stage[];
        try {
            stages = parsePlanStages(readIfExists(path.join(runDir, "plan.md")) ?? "");
        } catch (e) {
            return fail(`cannot parse plan stages: ${(e as Error).message}`);
        }
        const handoffPath = path.join(runDir, "handoff.md");
        for (const stage of stages) {
            const handoff = readIfExists(handoffPath) ?? "_(none yet)_";
            const cumulativeDiff = gitDiffStat(runDir);
            const res = await harness.runPhase({
                label: `implement:${stage.id}`,
                prompt: renderTemplate("06-implement", {
                    task: opts.task,
                    stage: JSON.stringify(stage, null, 2),
                    handoff,
                    cumulativeDiff,
                    contextNeeded: stage.context_needed.join(", "),
                    priorErrors: "",
                }),
                runDir,
                logPath,
                readOnly: false,
                model: resolveModel("implement", opts.judgeModel, opts),
            });
            // The agent runs gate.commands in-session and reports the result (low-maintenance path).
            const reportedPass = /STAGE_RESULT:\s*pass/i.test(res.finalText);
            logEvent(logPath, { kind: "stage_result", stage: stage.id, reportedPass });
            if (!reportedPass) {
                setPhase(runDir, state, "implement", "failed");
                return fail(`stage ${stage.id} reported a failing gate; halting. See execution-log.jsonl.`);
            }
        }
        setPhase(runDir, state, "implement", "completed");
    }

    saveState(runDir, state);
    return { exitCode: 0, runId, runDir, message: `Run ${runId} complete.` };
}

function resolveModel(phase: string, _judgeModel: string | undefined, opts: RunOptions): string | undefined {
    void opts;
    return PHASE_MODELS[phase];
}

/**
 * Reflexive judge loop (§3.1): critique on an ALTERNATE model (read-only), then adjudicate/revise
 * on the author's model, then re-validate. A bad revision is left for the normal phase retry path
 * (we do not regress: revision only replaces the artifact if it still validates).
 */
export async function judgeArtifact(
    harness: Harness,
    runDir: string,
    logPath: string,
    artifactRel: string,
    opts: RunOptions,
    revalidate: () => ValidationResult,
): Promise<void> {
    const artifactName = path.basename(artifactRel).replace(/\.[^.]+$/, "");
    const critiqueRel = `critiques/${artifactName}.md`;
    const judgeModel = opts.judgeModel ?? PHASE_MODELS.critique;

    await harness.runPhase({
        label: `critique:${artifactName}`,
        prompt: renderTemplate("critique", { task: opts.task, artifactPath: artifactRel, artifactName }),
        runDir,
        logPath,
        readOnly: true,
        model: judgeModel,
    });
    if (!fs.existsSync(path.join(runDir, critiqueRel))) {
        logEvent(logPath, { kind: "judge_skipped", artifact: artifactRel, reason: "no critique produced" });
        return;
    }

    const before = readIfExists(path.join(runDir, artifactRel)) ?? "";
    await harness.runPhase({
        label: `revise:${artifactName}`,
        prompt: renderTemplate("revise", {
            task: opts.task,
            artifactPath: artifactRel,
            critiquePath: critiqueRel,
        }),
        runDir,
        logPath,
        readOnly: true,
        model: PHASE_MODELS.plan,
    });

    // Don't accept a revision that broke validation: roll back to the pre-revision content.
    const result = revalidate();
    if (!result.ok) {
        fs.writeFileSync(path.join(runDir, artifactRel), before);
        logEvent(logPath, { kind: "revision_rolled_back", artifact: artifactRel, errors: result.errors });
    } else {
        logEvent(logPath, { kind: "judge_complete", artifact: artifactRel });
    }
}

/** Fan out phase-4 research, honoring dependsOn ordering with bounded concurrency. */
async function fanOutResearch(
    harness: Harness,
    runDir: string,
    logPath: string,
    opts: RunOptions,
    subitems: SubItems,
    concurrency: number,
    maxRetries: number,
): Promise<boolean> {
    const items = subitems.items;
    const completed = new Set<string>();
    const failed = new Set<string>();
    const research = (id: string) => path.join(runDir, "research", `${id}.md`);

    while (completed.size + failed.size < items.length) {
        const ready = items.filter(
            (it) =>
                !completed.has(it.id) &&
                !failed.has(it.id) &&
                it.dependsOn.every((d) => completed.has(d) || !items.some((x) => x.id === d)),
        );
        if (ready.length === 0) {
            // remaining items are blocked by failed deps
            items.filter((it) => !completed.has(it.id)).forEach((it) => failed.add(it.id));
            break;
        }
        const batch = ready.slice(0, concurrency);
        const results = await Promise.all(
            batch.map(async (it) => {
                const dependsOnNote = it.dependsOn.length
                    ? it.dependsOn.map((d) => `research/${d}.md`).join(", ")
                    : "none";
                let priorErrors = "";
                for (let attempt = 0; attempt <= maxRetries; attempt++) {
                    await harness.runPhase({
                        label: `research-item:${it.id}${attempt ? ` (retry ${attempt})` : ""}`,
                        prompt: renderTemplate("04-research-item", {
                            task: opts.task,
                            item: JSON.stringify(it, null, 2),
                            itemId: it.id,
                            dependsOnNote,
                            researchNote: "",
                            priorErrors,
                        }),
                        runDir,
                        logPath,
                        readOnly: true,
                        model: PHASE_MODELS["research-item"],
                    });
                    if (fs.existsSync(research(it.id))) {
                        return { id: it.id, ok: true };
                    }
                    priorErrors = `\nYour previous attempt did not write research/${it.id}.md. Write it now.\n`;
                }
                return { id: it.id, ok: false };
            }),
        );
        for (const r of results) {
            if (r.ok) completed.add(r.id);
            else failed.add(r.id);
        }
    }
    return failed.size === 0;
}

/** Cumulative diff summary for the implement context pack (best-effort; empty when not a repo). */
function gitDiffStat(cwd: string): string {
    try {
        const r = spawnSync("git", ["diff", "--stat"], { cwd, encoding: "utf8" });
        return r.status === 0 ? r.stdout.trim() || "(no changes yet)" : "(diff unavailable)";
    } catch {
        return "(diff unavailable)";
    }
}

export { PHASE_ORDER };
export type { RunState };
