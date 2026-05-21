const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const REPO_URL = 'https://github.com/Azure/azure-rest-api-specs.git';
const FILES = ['package.json', 'package-lock.json'];
const DEST = process.argv[2]
  ? path.resolve(process.cwd(), process.argv[2])
  : path.resolve(__dirname, 'Microsoft.Widget', 'Widget');

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'specs-'));
try {
  execFileSync('git', ['clone', '--filter=blob:none', '--sparse', '--depth=1',
    '--branch', 'main', REPO_URL, tmp], { stdio: 'inherit' });
  execFileSync('git', ['-C', tmp, 'sparse-checkout', 'set', '--no-cone',
    '/*', '!/specification/'], { stdio: 'inherit' });
  fs.mkdirSync(DEST, { recursive: true });
  for (const f of FILES) fs.copyFileSync(path.join(tmp, f), path.join(DEST, f));
} finally {
  fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 5 });
}