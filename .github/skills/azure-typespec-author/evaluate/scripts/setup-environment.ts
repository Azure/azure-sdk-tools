/**
 * Sets up the local evaluation environment:
 * 1. Selects the prebuilt MCP Vally environment.
 * 2. Builds the tools repository's MCP binaries, or verifies caller-supplied artifacts.
 * 3. Generates standalone fixtures from a separate azure-rest-api-specs checkout.
 * 4. Installs fixture dependencies with the spec repository's pinned pnpm.
 * 5. Verifies fixture dependencies and outputs variables for local Vally runs.
 */
import { execFileSync } from "node:child_process";
import type { ExecFileSyncOptions } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../../../../..");
const evalsDir = path.resolve(scriptDir, "../evals");
const fixtureRoot = path.resolve(scriptDir, "../fixtures/Microsoft.Widget/Widget");
const evalToolsDir = path.join(repoRoot, "eng", "common", "scripts", "eval");

const { values } = parseArgs({ options: {
  "mcp-kind": { type: "string" },
  "mcp-root": { type: "string" },
  "spec-root": { type: "string" },
} });
const mcpKind = values["mcp-kind"];
const suppliedMcpRoot = values["mcp-root"] ?? process.env.AZSDK_EVAL_MCP_ROOT;
const mcpRoot = path.resolve(suppliedMcpRoot || path.join(repoRoot, "artifacts", "mcp"));
if (mcpKind !== "live" && mcpKind !== "mock") {
  process.stderr.write(
    "Usage: node scripts/setup-environment.ts --mcp-kind live|mock [--mcp-root <path>] [--spec-root <path>]\n",
  );
  process.exit(1);
}

function run(command: string, args: string[], options: ExecFileSyncOptions = {}) {
  process.stderr.write(`> ${command} ${args.join(" ")}\n`);
  execFileSync(command, args, {
    stdio: ["inherit", 2, "inherit"],
    ...options,
  });
}

function runPnpm(args: string[], options: ExecFileSyncOptions = {}) {
  if (process.platform === "win32") {
    run("cmd.exe", ["/d", "/s", "/c", `pnpm ${args.join(" ")}`], options);
    return;
  }
  run("pnpm", args, options);
}

function runNpm(args: string[], options: ExecFileSyncOptions = {}) {
  if (process.platform === "win32") {
    run("cmd.exe", ["/d", "/s", "/c", `npm ${args.join(" ")}`], options);
    return;
  }
  run("npm", args, options);
}

if (!suppliedMcpRoot) {
  process.stderr.write("==> Building MCP binaries from azure-sdk-tools...\n");
  for (const [project, directory] of [["Azure.Sdk.Tools.Cli", "cli"], ["Azure.Sdk.Tools.Mock", "mock"]]) {
    run("dotnet", ["build", path.join(repoRoot, "tools", "azsdk-cli", project),
      "-c", "Release", "-o", path.join(mcpRoot, directory), "--nologo",
      `/p:CopilotNpmRegistryUrl=${process.env.COPILOT_NPM_REGISTRY_URL || "https://packagefeedproxy.microsoft.io/npm/"}`],
    { cwd: repoRoot });
  }
}
const mcpBinary = path.join(mcpRoot, mcpKind === "live" ? "cli" : "mock",
  mcpKind === "live" ? "azsdk.dll" : "azsdk-mock.dll");
if (!fs.statSync(mcpBinary, { throwIfNoEntry: false })?.isFile()) {
  throw new Error(`Prebuilt ${mcpKind} MCP artifact is missing or is not a file: ${mcpBinary}`);
}
process.stderr.write(`==> Using MCP artifact ${mcpBinary}.\n`);

const evalEnvironment = mcpKind === "live" ? "azsdk-mcp-local" : "azsdk-mcp-mock-local";
for (const fileName of fs.readdirSync(evalsDir).filter((name) => name.endsWith(".eval.yaml"))) {
  const filePath = path.join(evalsDir, fileName);
  const content = fs.readFileSync(filePath, "utf8");
  const environmentPattern = /^agent_environment:[ \t]+\S+[ \t]*(?=\r?$)/m;
  if (!environmentPattern.test(content)) {
    continue;
  }
  const updatedContent = content.replace(environmentPattern, `agent_environment: ${evalEnvironment}`);
  if (updatedContent !== content) {
    fs.writeFileSync(filePath, updatedContent);
  }
}
process.stderr.write(`==> Using local prebuilt MCP startup (${evalEnvironment}).\n`);

process.stderr.write("==> Installing the repository-pinned Vally toolchain...\n");
runNpm(["ci", "--no-audit", "--no-fund"], { cwd: evalToolsDir });
const vallyExecutable = path.join(
  evalToolsDir,
  "node_modules",
  ".bin",
  process.platform === "win32" ? "vally.cmd" : "vally",
);
const vallyVersion =
  process.platform === "win32"
    ? execFileSync("cmd.exe", ["/d", "/s", "/c", "node_modules\\.bin\\vally.cmd --version"], {
        cwd: evalToolsDir,
        encoding: "utf8",
      }).trim()
    : execFileSync(vallyExecutable, ["--version"], { encoding: "utf8" }).trim();
const toolchain: { devDependencies: Record<string, string> } =
  JSON.parse(fs.readFileSync(path.join(evalToolsDir, "package.json"), "utf8"));
const expectedVallyVersion = toolchain.devDependencies["@microsoft/vally-cli"];
if (vallyVersion !== expectedVallyVersion) {
  throw new Error(
    `The repository-pinned Vally executable must be ${expectedVallyVersion}; resolved ${vallyVersion || "no version"} from ${vallyExecutable}.`,
  );
}
process.stderr.write(`==> Using Vally ${vallyVersion} from ${vallyExecutable}.\n`);

const specArgs = values["spec-root"] ? ["--spec-root", path.resolve(values["spec-root"])] : [];
process.stderr.write("==> Preparing fixture files from azure-rest-api-specs...\n");
run(process.execPath, [path.join(scriptDir, "setup-fixture-files.ts"), ...specArgs], {
  cwd: repoRoot,
});
process.stderr.write("==> Installing standalone fixture dependencies with the pinned pnpm...\n");
run(process.execPath, [path.join(scriptDir, "install-pnpm.ts"), ...specArgs], { cwd: repoRoot });
runPnpm(["install"], { cwd: fixtureRoot });
run(process.execPath, [path.join(scriptDir, "check-node-dependencies.ts"), "packages"], {
  cwd: fixtureRoot,
  env: {
    ...process.env,
    FIXTURE_NODE_MODULES: path.join(fixtureRoot, "node_modules"),
  },
});

const nodeModules = path.join(fixtureRoot, "node_modules");
const nodeBin = path.join(nodeModules, ".bin");
const evalToolsBin = path.dirname(vallyExecutable);
const shell = process.env.SHELL ?? "";
const isPowerShell = !shell && process.platform === "win32" && !process.env.BASH;
if (isPowerShell) {
  console.log(`$env:AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`$env:AZSDK_EVAL_MCP_ROOT="${mcpRoot}"`);
  console.log(`$env:FIXTURE_NODE_MODULES="${nodeModules}"`);
  console.log(`$env:PATH="${evalToolsBin};${nodeBin};$env:PATH"`);
} else {
  console.log(`export AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`export AZSDK_EVAL_MCP_ROOT="${mcpRoot}"`);
  console.log(`export FIXTURE_NODE_MODULES="${nodeModules}"`);
  console.log(`export PATH="${evalToolsBin}:${nodeBin}:$PATH"`);
}
process.stderr.write("==> Setup complete.\n");
