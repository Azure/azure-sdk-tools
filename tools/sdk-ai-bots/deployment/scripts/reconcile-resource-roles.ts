import { buildAzdEnvironmentValues, loadEnvironmentSuite } from "../hooks/lib/env-suite.js";
import { reconcileResourceRoles } from "../hooks/lib/resource-roles.js";
import { runCli } from "./lib/cli.js";

runCli(() => {
  if (process.argv[2] !== "deployment" || process.argv.length !== 3) {
    throw new Error("Usage: reconcile-resource-roles.ts deployment");
  }
  const defaults = process.env.AZURE_ENV_NAME
    ? buildAzdEnvironmentValues(loadEnvironmentSuite(), process.env.AZURE_ENV_NAME) : {};
  reconcileResourceRoles("deployment", { ...defaults, ...process.env });
});
