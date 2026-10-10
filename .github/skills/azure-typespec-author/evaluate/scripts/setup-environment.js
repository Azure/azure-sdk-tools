/**
 * Sets up the evaluation fixture environment:
 * 1. Selects the prebuilt MCP Vally environment.
 * 2. Builds the live and mock MCP binaries into artifacts/mcp.
 * 3. Sparse-clones azure-rest-api-specs and prepares its maintained fixtures.
 * 4. Installs the spec repository's pinned pnpm and frozen fixture dependencies.
 * 5. Outputs the shell commands and PATH update used by Vally and its graders.
 *
 * Usage:
 *   node scripts/setup-environment.js --mcp-kind live
 *   node scripts/setup-environment.js --mcp-kind mock
 *   eval $(node scripts/setup-environment.js --mcp-kind live)
 *
 * Linux/WSL setup:
 *   - Use Linux-native Node.js 24.14.1 or newer; do not resolve it from /mnt/c Windows shims.
 *   - Install the .NET SDK plus the .NET 8 and ASP.NET Core 8 runtimes required by the
 *     prebuilt MCP servers (`dotnet --list-runtimes` must list both 8.x shared frameworks).
 *   - Evaluate this script in the current shell so AZSDK_EVAL_REPO_ROOT and
 *     FIXTURE_NODE_MODULES remain available to Vally.
 *
 * Vally cannot be installed locally under some managed-device
 * permissions. It depends on the native better-sqlite3 module, whose fallback
 * installation invokes node-gyp. This environment blocks direct registry.npmjs.org
 * downloads and does not permit elevating to install the required system build tools.
 * Use the repository-pinned installation prepared through the approved package feed.
 *
 * On Windows (PowerShell):
 *   node scripts/setup-environment.js | Invoke-Expression
 */
const { execSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const scriptDir = __dirname;
const repoRoot = path.resolve(scriptDir, '..', '..', '..', '..', '..');
const evalsDir = path.resolve(scriptDir, '..', 'evals');
const widgetDir = path.resolve(scriptDir, '..', 'fixtures', 'Microsoft.Widget', 'Widget');
const copilotNpmRegistryUrl = process.env.COPILOT_NPM_REGISTRY_URL || 'https://packagefeedproxy.microsoft.io/npm/';
const pnpmCommand = process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm';

const kindArgIndex = process.argv.indexOf('--mcp-kind');
const mcpKind = kindArgIndex === -1 ? undefined : process.argv[kindArgIndex + 1];
if (!['live', 'mock'].includes(mcpKind) || process.argv.length !== 4) {
  process.stderr.write('Usage: node scripts/setup-environment.js --mcp-kind live|mock\n');
  process.exit(1);
}

function run(command, options = {}) {
  execSync(command, { stdio: ['inherit', 2, 'inherit'], ...options });
}

// Step 1: Select the local prebuilt MCP environment.
const evalEnvironment = mcpKind === 'live' ? 'azsdk-mcp-local' : 'azsdk-mcp-mock-local';
for (const fileName of fs.readdirSync(evalsDir).filter((name) => name.endsWith('.eval.yaml'))) {
  const filePath = path.join(evalsDir, fileName);
  const content = fs.readFileSync(filePath, 'utf8');
  const environmentPattern = /^environment:[ \t]+\S+[ \t]*(?=\r?$)/m;
  if (!environmentPattern.test(content)) {
    continue;
  }
  const updatedContent = content.replace(environmentPattern, `environment: ${evalEnvironment}`);
  if (updatedContent !== content) {
    fs.writeFileSync(filePath, updatedContent);
  }
}
process.stderr.write(`==> Using local prebuilt MCP startup (${evalEnvironment}).\n`);

// Step 2: Build binaries used by the Vally environment.
process.stderr.write('==> Building prebuilt MCP binaries into artifacts/mcp...\n');
run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli -c Release -o artifacts/mcp/cli --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });
run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock -c Release -o artifacts/mcp/mock --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });

// Step 3: Prepare the spec repository and fixture files.
process.stderr.write('==> Preparing fixture files from azure-rest-api-specs...\n');
run(`node ${JSON.stringify(path.join(scriptDir, 'setup-fixture-files.js'))}`);

// Step 4: Install the pinned pnpm and frozen fixture dependencies.
process.stderr.write('==> Installing frozen fixture dependencies with pnpm...\n');
run(`node ${JSON.stringify(path.join(scriptDir, 'install-pnpm.js'))}`);
run(`${pnpmCommand} install --frozen-lockfile`, { cwd: widgetDir });

// Step 5: Output env var setters (stdout only, so eval/Invoke-Expression works).
const nodeModules = path.join(widgetDir, 'node_modules');
const nodeBin = path.join(nodeModules, '.bin');
const shell = process.env.SHELL || '';
const isPowerShell = !shell && process.platform === 'win32' && !process.env.BASH;
if (isPowerShell) {
  console.log(`$env:AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`$env:FIXTURE_NODE_MODULES="${nodeModules}"`);
  console.log(`$env:PATH="${nodeBin};$env:PATH"`);
} else {
  console.log(`export AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`export FIXTURE_NODE_MODULES="${nodeModules}"`);
  console.log(`export PATH="${nodeBin}:$PATH"`);
}
process.stderr.write('==> Setup complete.\n');
