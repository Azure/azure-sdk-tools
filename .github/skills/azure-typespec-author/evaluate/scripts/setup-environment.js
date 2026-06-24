/**
 * Sets up the evaluation fixture environment:
 * 1. Runs setup-fixture-files.js to download package.json / package-lock.json
 *    and the live .github/copilot-instructions.md from azure-rest-api-specs.
 * 2. Runs npm ci in the Widget fixture directory.
 * 3. Outputs the shell command to set FIXTURE_NODE_MODULES.
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
const widgetDir = path.resolve(scriptDir, '..', 'fixtures', 'Microsoft.Widget', 'Widget');

// Step 1: Download package files and copilot-instructions.md
process.stderr.write('==> Downloading package files and copilot-instructions.md from azure-rest-api-specs...\n');
execSync(`node ${JSON.stringify(path.join(scriptDir, 'setup-fixture-files.js'))}`, { stdio: ['inherit', 2, 'inherit'] });

// Step 2: Run npm ci
process.stderr.write(`==> Running npm ci in ${widgetDir} ...\n`);
execSync('npm ci', { cwd: widgetDir, stdio: ['inherit', 2, 'inherit'] });

// Step 3: Output env var setter (stdout only, so eval/Invoke-Expression works)
const nodeModules = path.join(widgetDir, 'node_modules');
const shell = process.env.SHELL || '';
const isPowerShell = !shell && process.platform === 'win32' && !process.env.BASH;
if (isPowerShell) {
  console.log(`$env:FIXTURE_NODE_MODULES="${nodeModules}"`);
} else {
  console.log(`export FIXTURE_NODE_MODULES="${nodeModules}"`);
}
process.stderr.write('==> Setup complete.\n');
