# T0 Capability Spike — Findings (G0)

Verified **live** against `@github/copilot-sdk@1.0.4` (Node 25, logged-in user auth) via
`spike.mjs` + `tool-test.mjs`. Model used: `claude-haiku-4.5`.

| ID | Capability | Result | Evidence |
| --- | --- | --- | --- |
| T0.1 | `createSession()` headless, streams, stops cleanly; `approveAll` | ✅ | sessions created/answered/disconnected; `client.stop()` clean |
| T0.2 | **Isolation** via single-use nonce | ✅ | nonce `ISO-…` told to session A; fresh session B replied `NONE` (no bleed) |
| **T0.3** | **`createSession()` accepts `hooks` (`onPreToolUse`)** | ✅ **YES** | `hooks.onPreToolUse` returning `{permissionDecision:"deny"}` blocked a shell call; agent reported it could not run the command |
| T0.4 | ≥3 concurrent sessions (phase-4 fan-out) | ✅ | 3 simultaneous sessions each returned correct distinct answers |
| T0.5 | Spawn **named built-in** agent (e.g. `rubber-duck`) + per-session model override | ⚠️ Partial | per-session `model` override ✅; but `SessionConfig.agent` **only references `customAgents[]`** you define — no direct built-in-agent spawn by name |

Custom tool: `defineTool(name, { parameters, handler, skipPermission:true })` works; the agent
called `write_artifact` and the handler fired. `defineTool`'s name is the **first positional arg**.

## Decisions gated by this spike

1. **Enforcement path = hooks (`onPreToolUse` → `deny`).** T0.3 is YES, so read-only enforcement
   for non-impl phases is a hook that denies edit/shell tools. **`FALLBACK.md` read-only-checkout
   machinery is dropped entirely.** The post-phase git-diff guard remains as a cheap backstop only.
2. **Judge critique uses the `critique.md` template on an *alternate model*** (not a built-in
   `rubber-duck` spawn), because `agent` can't name a built-in agent. Per-session model override —
   the part actually required for judge diversity — is confirmed working.
3. **`write_artifact`** is a `defineTool` custom tool with `skipPermission: true`.
4. **Autonomous runs** use `onPermissionRequest: approveAll`.

## API shape confirmed (from `dist/*.d.ts`, authoritative)

- `client.createSession(config: SessionConfig)`; `SessionConfig extends SessionConfigBase`.
- `SessionConfigBase.model?: string`; `SessionConfig.hooks?: SessionHooks`
  (`onPreToolUse`, `onPostToolUse`, `onPostToolUseFailure`, `onErrorOccurred`).
- `onErrorOccurred` returns `{ errorHandling?: "retry" | "skip" | "abort" }` → transient retries.
- `onPreToolUse` returns `{ permissionDecision: "allow"|"deny"|"ask", permissionDecisionReason? }`.
- `Tool.skipPermission?: boolean`; `approveAll: PermissionHandler`.
- Events: `assistant.message`, `session.idle` (+ typed `tool.execution_complete`, etc.).
- Session lifecycle: `session.send({prompt})`, `session.disconnect()`, `client.stop()`.

**G0 = PASS.** Isolation + permissions confirmed; enforcement path chosen (hooks). No `FALLBACK.md`.
