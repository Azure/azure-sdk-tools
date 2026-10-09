# azsdk-cli Review Rules

`tools/azsdk-cli/AGENTS.md` is the architectural source of truth and is owned
by the azsdk-cli team. Use the changed surface to select additional context;
do not apply every check mechanically.

| Changed surface | Inspect for behavioral risk | Repository context |
| --- | --- | --- |
| Tool or command | CLI/MCP behavior parity, cancellation through the full call chain, and meaningful failure responses | `Commands/`, `Tools/Core/`, corresponding tool tests |
| Response model | Consistent plain, JSON, and MCP behavior; accurate errors, status, exit code, and `NextSteps` | `Models/Responses/`, `Helpers/OutputHelper.cs`, response tests |
| Service or helper | Correct DI lifetime, unsafe shared state, resource cleanup, and bounded external work | `Services/ServiceRegistrations.cs`, callers, helper/service tests |
| Process, HTTP, polling, or retry code | Cancellation, timeout or termination bound, retry limit, and surfaced terminal failure | `docs/process-calling.md`, process helpers, failure-path tests |
| MCP mock or tool contract | Mock signature and response behavior still match the live tool | live tool, `Azure.Sdk.Tools.Mock/`, tool evals |
| Language-specific behavior | Supported-language coverage, explicit unsupported behavior, and consistent fallback | `Services/Languages/`, `docs/per-language.md`, language tests |
| Feature governed by a spec | Implementation and failure behavior still satisfy the applicable design | `docs/specs/`, implementation, tests |

Look for omissions across files when a changed contract requires registration,
serialization, mock, documentation, or test updates. Require a concrete
failure path before treating missing coverage as a finding.

MCP001-MCP007, compiler errors, formatting, and exact CI failures are owned by
deterministic checks. Do not restate them as skill findings. Investigate
behavior beyond those checks, such as a cancellation token that is forwarded
but ineffective or a registered service with an unsafe lifetime.
