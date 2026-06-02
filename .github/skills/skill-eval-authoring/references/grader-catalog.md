# Grader catalog (Vally 0.5.0)

Verified against `eng/skill-eval/node_modules/@microsoft/vally/dist/graders/`.
Both static graders (deterministic, code-based) and the LLM judge are listed.
Aliases are explicit.

## Static graders

| Grader | Purpose | Typical use |
|--------|---------|-------------|
| `skill-invocation` | `required` / `disallowed` skills loaded during the run | Trigger and anti-trigger tests |
| `tool-calls` | `required` / `disallowed` tool calls; `name` is regex; `command` / `path` arg regex supported | Every capability test |
| `output-contains` | Substring search on assistant output; supports `negate: true` | Cheap sanity, never sole grader |
| `output-not-contains` | Alias of `output-contains` with `negate: true` | Negative sanity |
| `output-matches` | Regex search on assistant output | Preferred capability output-shape grader |
| `file-exists` | A file exists in the workspace after the run | Skills that scaffold files |
| `file-contains` | Substring search in a workspace file | Skills that edit files |
| `file-matches` | Regex search in a workspace file | Skills that edit files |
| `completed` | Trajectory finished without unhandled error | Liveness baseline |
| `run-command` | Exec a shell command; non-zero exit = fail | Build/lint verification |
| `program` | Exec a script with stdin/stdout JSON contract | Custom verifiers |
| `metric-threshold` (aliases: `token-budget`, `tool-call-count`, `turn-count`, `error-count`, `wall-time`) | Cap a metric collected during the run | Nightly performance tier |

## LLM graders

| Grader | Purpose | Cost | Notes |
|--------|---------|------|-------|
| `prompt` | Free-form LLM judge with a custom rubric | High | Use only when regex/tool-calls cannot express the assertion |
| `pairwise` | A/B comparison of two trajectories | High | Only valid in `vally compare`, not `vally eval` |

## Picking a grader

```
Need to check routing?            → skill-invocation
Need to check the right tool?     → tool-calls
Need to check the wrong tool didn't fire? → tool-calls (disallowed)
Need to check the answer mentions X? → output-contains
Need to check the answer is structurally correct? → output-matches
Need to check a file was created?  → file-exists / file-matches
Need to check the build still passes after a code edit? → run-command
Need to check performance?         → metric-threshold
Anything else?                     → prompt (LLM, last resort)
```

## Hidden behaviors worth knowing

- **`scoring.weights`** is parsed but not currently applied per-grader by the
  runtime. Set `scoring.threshold` for a pass/fail bar; do not rely on weights
  for partial credit.
- **`scoring.threshold` defaults to 0** if unset → every stimulus passes. Lint
  fails missing thresholds for that reason.
- **`tool-calls.name`** is a regex. An empty pattern would match every tool;
  the grader explicitly rejects this to avoid silent passes.
- **`tool-calls`** receives the tool name as recorded by the executor. The
  copilot-sdk executor prefixes MCP tools with the server alias
  (`azure-sdk-mcp-azsdk_create_release_plan`). Bare names work because regex
  matches a substring; prefer bare for portability.
- **`environment.git.source`** does NOT expand env vars; use a literal URL or
  set up the fixture in `workDir` instead.
