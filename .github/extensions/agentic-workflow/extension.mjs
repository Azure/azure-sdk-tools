// Optional interactive front-door for the agentic workflow (thinnerplan T3.2).
//
// Thin shim ONLY: it registers a `/agentic-workflow` slash command + a `run_agentic_workflow`
// tool inside an interactive Copilot CLI session, and delegates to the *built* orchestrator. It
// contains no orchestration logic of its own — re-point the import and it still works.
import { joinSession } from "@github/copilot-sdk/extension";
import { fileURLToPath } from "node:url";
import * as path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
// Resolve the built tool relative to this file: repo/.github/extensions/agentic-workflow -> tool.
const TOOL_DIST = path.resolve(here, "..", "..", "..", "tools", "agentic-workflow", "dist", "index.js");

let sessionRef;

/** Parse the free-form arg string into RunOptions for the orchestrator. */
function parseArgs(argstr) {
    const tokens = (argstr ?? "").trim().match(/(?:[^\s"]+|"[^"]*")+/g) ?? [];
    const opts = { task: "", judge: true };
    const taskWords = [];
    for (let i = 0; i < tokens.length; i++) {
        const t = tokens[i].replace(/^"|"$/g, "");
        if (t === "--simple") opts.simple = true;
        else if (t === "--no-judge") opts.judge = false;
        else if (t === "--judge-model") opts.judgeModel = tokens[++i]?.replace(/^"|"$/g, "");
        else if (t === "--run-id") opts.runId = tokens[++i]?.replace(/^"|"$/g, "");
        else taskWords.push(t);
    }
    opts.task = taskWords.join(" ");
    return opts;
}

async function execute(argstr) {
    const session = sessionRef;
    const opts = parseArgs(argstr);
    if (!opts.task) {
        await session?.log("agentic-workflow: provide a task, e.g. /agentic-workflow Add CSV export --simple");
        return;
    }
    let runWorkflow, SdkHarness;
    try {
        ({ runWorkflow, SdkHarness } = await import(TOOL_DIST));
    } catch {
        await session?.log(
            `agentic-workflow: build the tool first (cd tools/agentic-workflow && npm run build). Looked for ${TOOL_DIST}`,
        );
        return;
    }
    await session?.log(`agentic-workflow: starting run for "${opts.task}"${opts.simple ? " (--simple)" : ""}…`);
    const harness = new SdkHarness({ workingDirectory: process.cwd() });
    try {
        const result = await runWorkflow(opts, harness);
        await session?.log(
            `agentic-workflow: ${result.message} (exit ${result.exitCode}) — artifacts at ${result.runDir}`,
        );
    } catch (err) {
        await session?.log(`agentic-workflow: run failed — ${err?.message ?? err}`);
    } finally {
        await harness.stop();
    }
}

sessionRef = await joinSession({
    commands: [
        {
            name: "agentic-workflow",
            description: "Run the research -> plan -> implement workflow on a task.",
            handler: (ctx) => execute(ctx.args),
        },
    ],
    tools: [
        {
            name: "run_agentic_workflow",
            description:
                "Run the agentic research -> plan -> implement workflow headlessly on a task " +
                "description. Accepts the same flags as the CLI (--simple, --no-judge, --judge-model).",
            parameters: {
                type: "object",
                properties: {
                    task: { type: "string", description: "The task description" },
                    args: { type: "string", description: "Optional flags, e.g. --simple --no-judge" },
                },
                required: ["task"],
            },
            handler: async (a) => {
                await execute(`${a?.task ?? ""} ${a?.args ?? ""}`);
                return { content: [{ type: "text", text: "agentic-workflow run dispatched" }] };
            },
        },
    ],
});
