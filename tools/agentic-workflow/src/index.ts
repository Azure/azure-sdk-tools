/**
 * Public entry: programmatic API for the agentic workflow.
 * The CLI (`cli.ts`) and the extension front-door both call `runWorkflow`.
 */
export { runWorkflow, type RunResult } from "./orchestrator.js";
export { SdkHarness, type Harness, type PhaseRequest } from "./harness.js";
export type { RunOptions, RunState } from "./types.js";
