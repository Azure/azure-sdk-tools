/**
 * Sets up the evaluation fixture environment:
 * 1. Builds the live and mock MCP binaries into artifacts/mcp.
 * 2. Runs setup-fixture-files.js to download package.json / package-lock.json
 *    and the live .github/copilot-instructions.md from azure-rest-api-specs.
 * 3. Runs npm ci in the Widget fixture directory.
 * 4. Outputs the shell commands to set AZSDK_EVAL_REPO_ROOT and FIXTURE_NODE_MODULES.
 *
 * Usage:
 *   node scripts/setup-environment.js          # prints export command
 *   eval $(node scripts/setup-environment.js)  # sets env var in current shell
 *
 * On Windows (PowerShell):
 *   node scripts/setup-environment.js | Invoke-Expression
 */
const { execSync } = require('node:child_process');
const path = require('node:path');

const scriptDir = __dirname;
const repoRoot = path.resolve(scriptDir, '..', '..', '..', '..', '..');
const widgetDir = path.resolve(scriptDir, '..', 'fixtures', 'Microsoft.Widget', 'Widget');
const copilotNpmRegistryUrl = process.env.COPILOT_NPM_REGISTRY_URL || 'https://packagefeedproxy.microsoft.io/npm/';

function run(command, options = {}) {
  execSync(command, { stdio: ['inherit', 2, 'inherit'], ...options });
}

// Step 1: Build MCP binaries used by the default Vally environments.
process.stderr.write('==> Building prebuilt MCP binaries into artifacts/mcp...\n');
run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli -c Release -o artifacts/mcp/cli --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });
run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock -c Release -o artifacts/mcp/mock --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });

// Step 2: Download package files and copilot-instructions.md.
process.stderr.write('==> Downloading package files and copilot-instructions.md from azure-rest-api-specs...\n');
run(`node ${JSON.stringify(path.join(scriptDir, 'setup-fixture-files.js'))}`);

// Step 3: Run npm ci.
process.stderr.write(`==> Running npm ci in ${widgetDir} ...\n`);
run('npm ci', { cwd: widgetDir });

// Step 4: Output env var setters (stdout only, so eval/Invoke-Expression works).
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
