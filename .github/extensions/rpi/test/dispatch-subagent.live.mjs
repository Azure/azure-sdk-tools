// Live test: prove a real workflow PHASE runs as its OWN fresh-context sub-agent (the change that
// replaced agent.select()+sendAndWait() + /compact with tasks.startAgent). It reuses the extension's
// actual joinConfig.customAgents so the agent spawned here (agentType == a phase agent) is exactly
// what dispatch() spawns at runtime, and mirrors runSubAgent()'s spawn -> poll -> parse loop.
//
// It verifies the properties the feature depends on:
//   1. a phase custom-agent can be spawned by name via tasks.startAgent (fresh context window),
//   2. it can DO work (write an artifact under the run dir) — i.e. it has tool access,
//   3. its activity STREAMS to the host session (agentId-tagged events) so the user sees progress,
//   4. it self-reports with the PHASE_RESULT sentinel we parse.
//
// Run:  node test/dispatch-subagent.live.mjs
// Requires: an authenticated Copilot CLI (this box is logged in via ~/.copilot).

import { CopilotClient } from "@github/copilot-sdk";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

// Must be set before importing extension.mjs so the module does not try to join a live host.
process.env.AW_SKIP_JOIN = "1";
const { parsePhaseResult, joinConfig, firstLine, toolArgHint } = await import("../extension.mjs");

const log = (...a) => console.log("[dispatch-test]", ...a);
const client = new CopilotClient({ logLevel: "warning" });
const TERMINAL = new Set(["completed", "failed", "cancelled", "idle"]);

async function main() {
    await client.start();
    // Spin up a host session with the extension's real per-phase custom agents.
    const session = await client.createSession({ customAgents: joinConfig.customAgents });

    const runDir = fs.mkdtempSync(path.join(os.tmpdir(), "aw-dispatch-"));
    const artifact = path.join(runDir, "assumptions.md");
    const phaseAgent = "rpi-assumptions"; // any registered phase agent exercises the same path

    // Mirror registerSubAgentRelay(): sub-agent events arrive on the host stream tagged with a
    // non-empty agentId. Collect the same lines the extension would forward to the user, proving the
    // user sees live activity while the sub-agent runs (the whole point of the streaming change).
    const relayed = [];
    session.on("assistant.intent", (ev) => { if (ev.agentId) relayed.push(`… ${firstLine(ev.data?.intent, 160)}`); });
    session.on("tool.execution_start", (ev) => { if (ev.agentId) relayed.push(`· ${ev.data?.toolName ?? "tool"}${toolArgHint(ev.data)}`); });
    session.on("assistant.message", (ev) => { if (ev.agentId && firstLine(ev.data?.content)) relayed.push(`💬 ${firstLine(ev.data?.content, 200)}`); });

    log(`spawning fresh-context sub-agent agentType=${phaseAgent}…`);
    const { agentId } = await session.rpc.tasks.startAgent({
        agentType: phaseAgent,
        name: "assumptions-1",
        description: "rpi assumptions phase (live test)",
        prompt:
            `This is a connectivity + capability check for the assumptions phase.\n` +
            `First read this directory with a tool, then write a one-line markdown file to ` +
            `\`${artifact}\` containing exactly: \`- assumption: the sky is blue\`.\n` +
            `Then end your turn with the single sentinel line: PHASE_RESULT: pass`,
    });
    log("spawned agentId =", agentId);

    const deadline = Date.now() + 3 * 60 * 1000;
    let info;
    while (Date.now() < deadline) {
        const { tasks } = await session.rpc.tasks.list();
        info = tasks.find((t) => t.id === agentId);
        if (info && TERMINAL.has(info.status)) break;
        await new Promise((r) => setTimeout(r, 3000));
    }
    await new Promise((r) => setTimeout(r, 1000)); // let trailing events flush

    const text = info?.result ?? info?.latestResponse ?? "";
    const { result, reason } = parsePhaseResult(text);
    const wrote = fs.existsSync(artifact);
    log(`final status=${info?.status} | PHASE_RESULT=${result}${reason ? ` (${reason})` : ""} | artifact written=${wrote}`);
    if (wrote) log("artifact contents:", JSON.stringify(fs.readFileSync(artifact, "utf8").trim()));
    log(`relayed ${relayed.length} activity line(s) the user would see:`);
    for (const line of relayed) log(`   ${line}`);

    const ok = TERMINAL.has(info?.status) && result === "pass" && wrote && relayed.length > 0;
    log(ok ? "✅ PASS: phase ran as a fresh-context sub-agent, streamed activity, wrote its artifact, and self-reported." : "❌ FAIL");

    try { fs.rmSync(runDir, { recursive: true, force: true }); } catch { /* best-effort */ }
    await client.stop().catch(() => {});
    process.exit(ok ? 0 : 1);
}

main().catch(async (e) => {
    console.error("[dispatch-test] error:", e?.stack ?? e);
    await client.stop().catch(() => {});
    process.exit(1);
});
