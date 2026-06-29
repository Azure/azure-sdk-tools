// T0 capability spike — verifies the SDK assumptions the whole design rests on.
// Run: node spike.mjs   (writes findings to stdout; auth via logged-in user)
import { CopilotClient, approveAll, defineTool } from "@github/copilot-sdk";

const MODEL = process.env.SPIKE_MODEL || "claude-haiku-4.5";
const results = {};

function ask(session, prompt, timeoutMs = 60000) {
  return new Promise(async (resolve, reject) => {
    let text = "";
    const timer = setTimeout(() => reject(new Error("timeout")), timeoutMs);
    session.on("assistant.message", (e) => { text += e.data.content ?? ""; });
    session.on("session.idle", () => { clearTimeout(timer); resolve(text); });
    await session.send({ prompt });
  });
}

const client = new CopilotClient();
await client.start();
try {
  // T0.1 + T0.2: isolation via single-use nonce
  const nonce = "ISO-" + Math.random().toString(36).slice(2, 10).toUpperCase();
  const a = await client.createSession({ model: MODEL, onPermissionRequest: approveAll });
  await ask(a, `Remember this exact token, I will quiz you later: ${nonce}. Reply only "ok".`);
  const b = await client.createSession({ model: MODEL, onPermissionRequest: approveAll });
  const bAns = await ask(b, `Earlier in THIS conversation I gave you a token starting with "ISO-". Output it verbatim. If you have never seen it, output exactly NONE.`);
  results.isolation = !bAns.includes(nonce);
  results.isolationDetail = { nonce, fresh_session_reply: bAns.trim().slice(0, 120) };
  await a.disconnect();
  await b.disconnect();

  // T0.3: hooks (onPreToolUse deny) accepted by createSession
  let denyFired = false;
  const h = await client.createSession({
    model: MODEL,
    onPermissionRequest: approveAll,
    hooks: {
      onPreToolUse: async (inv) => {
        if ((inv?.toolName || "").includes("shell") || (inv?.toolName || "").includes("bash")) {
          denyFired = true;
          return { permissionDecision: "deny", permissionDecisionReason: "spike: read-only phase" };
        }
        return { permissionDecision: "allow" };
      },
    },
  });
  const hAns = await ask(h, `Run the shell command "echo hello-from-shell" and tell me the output.`);
  results.hooks_accepted = true; // createSession did not throw on hooks
  results.hook_deny_fired = denyFired;
  results.hook_detail = hAns.trim().slice(0, 160);
  await h.disconnect();

  // T0.4: concurrency — 3 simultaneous sessions
  const conc = await Promise.all([1, 2, 3].map(async (n) => {
    const s = await client.createSession({ model: MODEL, onPermissionRequest: approveAll });
    const ans = await ask(s, `Reply with only the number ${n * 7}.`);
    await s.disconnect();
    return ans.includes(String(n * 7));
  }));
  results.concurrency = conc.every(Boolean);

  // T0.5: per-session model override + custom tool with skipPermission
  let toolCalled = false;
  const tool = defineTool({
    name: "write_artifact",
    description: "Spike artifact writer",
    parameters: { type: "object", properties: { content: { type: "string" } }, required: ["content"] },
    skipPermission: true,
    handler: async ({ content }) => { toolCalled = true; return { content: [{ type: "text", text: "written" }] }; },
  });
  const t = await client.createSession({ model: MODEL, onPermissionRequest: approveAll, tools: [tool] });
  await ask(t, `Call the write_artifact tool with content "hello". Then reply "done".`);
  results.custom_tool = toolCalled;
  results.model_override = true; // session created with explicit model
  await t.disconnect();
} catch (e) {
  results.error = String(e && e.stack ? e.stack : e);
} finally {
  await client.stop();
}
console.log("SPIKE_RESULTS " + JSON.stringify(results, null, 2));
