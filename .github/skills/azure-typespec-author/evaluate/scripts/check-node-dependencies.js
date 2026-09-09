const fs = require('node:fs');
const path = require('node:path');

const requiredPackages = [
  '@typespec/compiler',
  '@typespec/http',
  '@typespec/rest',
  '@typespec/versioning',
  '@azure-tools/typespec-azure-core',
  '@azure-tools/typespec-azure-resource-manager',
  '@azure-tools/typespec-autorest',
];

const mode = process.argv[2];

if (!mode || !['node_modules', 'packages'].includes(mode)) {
  console.error('[dependency-check] usage: node check-node-dependencies.js <node_modules|packages>');
  process.exit(2);
}

if (!fs.existsSync('node_modules')) {
  console.error('[dependency-check:node_modules] missing');
  process.exit(2);
}

if (mode === 'node_modules') {
  console.log('[dependency-check:node_modules] ok');
  process.exit(0);
}

const missing = requiredPackages.filter((packageName) => {
  return !fs.existsSync(path.join('node_modules', ...packageName.split('/'), 'package.json'));
});

if (missing.length) {
  console.error(`[dependency-check:packages] missing: ${missing.join(', ')}`);
  process.exit(2);
}

console.log('[dependency-check:packages] ok');