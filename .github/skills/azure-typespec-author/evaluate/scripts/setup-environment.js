/**
 * Sets up the evaluation fixture environment:
 * 1. Selects pipeline-style prebuilt MCP startup or legacy local project startup.
 * 2. Builds the live and mock MCP binaries into artifacts/mcp in pipeline mode.
 * 3. Runs setup-fixture-files.js to download package.json / package-lock.json
 *    and the live .github/copilot-instructions.md from azure-rest-api-specs.
 * 4. Runs npm ci in the Widget fixture directory.
 * 5. Outputs the shell commands to set AZSDK_EVAL_REPO_ROOT and FIXTURE_NODE_MODULES.
 *
 * Usage:
 *   node scripts/setup-environment.js                       # pipeline MCP mode
 *   node scripts/setup-environment.js --mcp-mode local      # legacy local MCP mode
 *   eval $(node scripts/setup-environment.js)               # sets env vars in current shell
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

const modeArgIndex = process.argv.indexOf('--mcp-mode');
const mcpMode = modeArgIndex === -1 ? 'pipeline' : process.argv[modeArgIndex + 1];
if (!['pipeline', 'local'].includes(mcpMode)) {
  process.stderr.write('Usage: node scripts/setup-environment.js [--mcp-mode pipeline|local]\n');
  process.exit(1);
}

function run(command, options = {}) {
  execSync(command, { stdio: ['inherit', 2, 'inherit'], ...options });
}

// Step 1: Keep eval specs aligned with the selected MCP startup mode.
const evalEnvironment = mcpMode === 'pipeline' ? 'azsdk-mcp' : 'azsdk-mcp-local';
for (const fileName of fs.readdirSync(evalsDir).filter((name) => name.endsWith('.eval.yaml'))) {
  const filePath = path.join(evalsDir, fileName);
  const content = fs.readFileSync(filePath, 'utf8');
  const environmentPattern = /^environment:[ \t]+\S+[ \t]*(?=\r?$)/m;
  if (!environmentPattern.test(content)) {
    throw new Error(`Missing root environment in ${filePath}`);
  }
  const updatedContent = content.replace(environmentPattern, `environment: ${evalEnvironment}`);
  if (updatedContent !== content) {
    fs.writeFileSync(filePath, updatedContent);
  }
}
process.stderr.write(`==> Using ${mcpMode} MCP startup (${evalEnvironment}).\n`);

// Step 2: Build binaries used by the pipeline-style Vally environment.
if (mcpMode === 'pipeline') {
  process.stderr.write('==> Building prebuilt MCP binaries into artifacts/mcp...\n');
  run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli -c Release -o artifacts/mcp/cli --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });
  run(`dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock -c Release -o artifacts/mcp/mock --nologo /p:CopilotNpmRegistryUrl=${copilotNpmRegistryUrl}`, { cwd: repoRoot });
}

// Step 3: Download package files and copilot-instructions.md.
process.stderr.write('==> Downloading package files and copilot-instructions.md from azure-rest-api-specs...\n');
run(`node ${JSON.stringify(path.join(scriptDir, 'setup-fixture-files.js'))}`);

// Step 4: Run npm ci.
process.stderr.write(`==> Running npm ci in ${widgetDir} ...\n`);
run('npm ci', { cwd: widgetDir });

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
