import { type CommandRunner, runCommand } from "../../scripts/lib/cli.js";

export function agentVersionMetadata(
  env: NodeJS.ProcessEnv,
  existing: Record<string, string> = {},
  run: CommandRunner = runCommand,
): Record<string, string> {
  const routing = env.ENABLE_AI_FOUNDRY_AGENT_ROUTING ?? "false";
  if (routing !== "true" && routing !== "false") {
    throw new Error("ENABLE_AI_FOUNDRY_AGENT_ROUTING must be true or false.");
  }
  const metadata: Record<string, string> = { ...existing, enableVnextExperience: "true" };
  delete metadata.branchname;
  if (routing === "true") {
    const sourceBranch = env.BUILD_SOURCEBRANCH?.trim() ||
      run("git", ["symbolic-ref", "--short", "HEAD"], { capture: true });
    const branch = sourceBranch.replace(/^refs\/heads\//, "").trim();
    if (!branch) {
      throw new Error("A source branch is required when hosted agent routing is enabled.");
    }
    metadata.branchname = branch;
  }
  return metadata;
}
