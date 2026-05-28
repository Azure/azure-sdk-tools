#!/usr/bin/env node
/*
 * Sparse-checkout https://github.com/Azure/azure-rest-api-specs (excluding the
 * `specification/` folder) and copy package.json + package-lock.json into the
 * Microsoft.Widget fixture directory.
 *
 * Cross-platform: runs on both Linux and Windows under Node.js (>=16).
 * Requires `git` in PATH.
 *
 * Destination directory:
 *   - With CLI arg:   resolved against the current working directory.
 *   - Without arg:    `<this-script-dir>/../fixtures/Microsoft.Widget/Widget`
 *
 * Invoke from an eval `commands:` entry (script copied into workDir via
 * environment.files), e.g.:
 *   commands:
 *     - node setup-package-files.js .
 */

const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const REPO_URL = 'https://github.com/Azure/azure-rest-api-specs.git';
const BRANCH = 'main';
const FILES = ['package.json', 'package-lock.json'];

const DEST = process.argv[2]
    ? path.resolve(process.cwd(), process.argv[2])
    : path.resolve(__dirname, '..', 'fixtures', 'Microsoft.Widget', 'Widget');

function run(cmd, args, opts = {}) {
    console.log(`> ${cmd} ${args.join(' ')}`);
    execFileSync(cmd, args, { stdio: 'inherit', ...opts });
}

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'azure-rest-api-specs-'));
console.log(`temp clone dir: ${tmp}`);

try {
    run('git', [
        'clone',
        '--filter=blob:none',
        '--sparse',
        '--depth=1',
        '--branch', BRANCH,
        REPO_URL,
        tmp,
    ]);

    // Non-cone sparse pattern: include everything at the root, then exclude
    // the large `specification/` folder.
    run('git', ['-C', tmp, 'sparse-checkout', 'set', '--no-cone', '/*', '!/specification/']);

    fs.mkdirSync(DEST, { recursive: true });
    for (const name of FILES) {
        const src = path.join(tmp, name);
        const dst = path.join(DEST, name);
        if (!fs.existsSync(src)) {
            throw new Error(`Expected file not present after sparse checkout: ${src}`);
        }
        fs.copyFileSync(src, dst);
        console.log(`copied ${name} -> ${dst}`);
    }
} finally {
    fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 5 });
}

// Strip local file: workspace references from package.json to prevent
// npm ci from creating eng/tools and .github/shared sub-packages.
const pkgPath = path.join(DEST, 'package.json');
const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
for (const section of ['dependencies', 'devDependencies']) {
    if (!pkg[section]) continue;
    for (const [name, version] of Object.entries(pkg[section])) {
        if (version.startsWith('file:')) {
            delete pkg[section][name];
        }
    }
}
if (pkg.workspaces) delete pkg.workspaces;
fs.writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n');
