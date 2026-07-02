// Live test: spawn a fresh-context sub-agent from inside a Copilot session using the SDK's
// tasks.startAgent RPC (the same surface a CLI extension gets via session.rpc.tasks).
//
// A sub-agent started this way runs in its OWN context window (fresh context) and returns a
// result string when it completes — unlike session.rpc.agent.select(), which merely switches the
// active custom agent within the current conversation timeline.
//
// Run:  node test/spawn-subagent.live.mjs
// Requires: an authenticated Copilot CLI (this box is logged in via ~/.copilot).

import { CopilotClient } from "@github/copilot-sdk";

const log = (...a) => console.log("[spawn-test]", ...a);

const client = new CopilotClient({ logLevel: "warning" });

async function main() {
    log("connecting to bundled runtime…");
    await client.start();

    log("creating a host session…");
    const session = await client.createSession({});

    log("startAgent: spawning a fresh-context 'general-purpose' sub-agent…");
    const { agentId } = await session.rpc.tasks.startAgent({
        agentType: "general-purpose",
        name: "ping",
        description: "connectivity check",
        prompt: "Reply with exactly the single word: PONG. Do not use any tools.",
    });
    log("spawned agentId =", agentId);

    // Poll until the sub-agent settles. Background agents rest at "idle" (responded, awaiting more
    // input) rather than "completed", so treat idle as a terminal state for this one-shot probe.
    const deadline = Date.now() + 3 * 60 * 1000;
    let info;
    while (Date.now() < deadline) {
        const { tasks } = await session.rpc.tasks.list();
        info = tasks.find((t) => t.id === agentId);
        if (info) {
            const prog = await session.rpc.tasks.getProgress({ id: agentId });
            const intent = prog?.progress?.latestIntent ?? "";
            log(`status=${info.status}${intent ? ` intent="${intent}"` : ""}`);
            if (["completed", "failed", "cancelled", "idle"].includes(info.status)) break;
        } else {
            log("task not yet in list…");
        }
        await new Promise((r) => setTimeout(r, 3000));
    }

    log("full task info =", JSON.stringify(info, null, 2));
    log("final status =", info?.status);
    const reply = info?.result ?? info?.latestResponse ?? null;
    log("reply =", JSON.stringify(reply));
    if (info?.error) log("error =", info.error);

    const ok = ["completed", "idle"].includes(info?.status) && /PONG/i.test(reply ?? "");
    log(ok ? "✅ PASS: fresh-context sub-agent spawned and returned a result." : "❌ FAIL");

    await client.stop().catch(() => {});
    process.exit(ok ? 0 : 1);
}

main().catch(async (e) => {
    console.error("[spawn-test] error:", e?.stack ?? e);
    await client.stop().catch(() => {});
    process.exit(1);
});
