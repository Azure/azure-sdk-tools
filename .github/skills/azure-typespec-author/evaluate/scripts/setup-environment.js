/**
 * Sets up the evaluation fixture environment:
 * 1. Selects the prebuilt MCP Vally environment.
 * 2. Builds the live and mock MCP binaries into artifacts/mcp.
 * 3. Runs setup-fixture-files.js to create a standalone npm package from the
 *    azure-rest-api-specs pnpm workspace and download its live instructions.
 * 4. Runs npm ci in the Widget fixture directory.
 * 5. Outputs the shell commands to set the environment variables used by Vally.
 *
 * Usage:
 *   node scripts/setup-environment.js --mcp-kind live
 *   node scripts/setup-environment.js --mcp-kind mock
 *   eval $(node scripts/setup-environment.js --mcp-kind live)
 *
 * Linux/WSL setup:
 *   - Use Linux-native Node.js 22 and npm; do not resolve them from /mnt/c Windows shims.
 *   - Install both the repository SDK and the .NET 8 runtime required by the net8.0
 *     prebuilt MCP servers (`dotnet --list-runtimes` must include Microsoft.NETCore.App 8.x).
 *   - Evaluate this script in the current shell so AZSDK_EVAL_REPO_ROOT and
 *     FIXTURE_NODE_MODULES remain available to Vally.
 *
 * Vally 0.14.0 cannot be installed locally under the current managed-device
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

// Step 3: Download package files and copilot-instructions.md.
process.stderr.write('==> Downloading package files and copilot-instructions.md from azure-rest-api-specs...\n');
run(`node ${JSON.stringify(path.join(scriptDir, 'setup-fixture-files.js'))}`);

// Step 4: Run npm ci.
process.stderr.write(`==> Running npm ci in ${widgetDir} ...\n`);
run('npm ci --legacy-peer-deps', { cwd: widgetDir });

// Step 5: Output env var setters (stdout only, so eval/Invoke-Expression works).
const nodeModules = path.join(widgetDir, 'node_modules');
const shell = process.env.SHELL || '';
const isPowerShell = !shell && process.platform === 'win32' && !process.env.BASH;
if (isPowerShell) {
  console.log(`$env:AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`$env:FIXTURE_NODE_MODULES="${nodeModules}"`);
} else {
  console.log(`export AZSDK_EVAL_REPO_ROOT="${repoRoot}"`);
  console.log(`export FIXTURE_NODE_MODULES="${nodeModules}"`);
}
process.stderr.write('==> Setup complete.\n');
