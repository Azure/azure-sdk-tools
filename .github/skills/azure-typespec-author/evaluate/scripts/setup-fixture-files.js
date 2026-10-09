#!/usr/bin/env node
/*
 * Sparse-checkout https://github.com/Azure/azure-rest-api-specs (excluding the
 * `specification/` folder), convert its pnpm workspace manifest into a standalone
 * npm package for the Microsoft.Widget fixture, and copy the live
 * `.github/copilot-instructions.md` into the instructions-test fixture directory.
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
 *     - node setup-fixture-files.js .
 */

const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const REPO_URL = 'https://github.com/Azure/azure-rest-api-specs.git';
const BRANCH = 'main';
const PACKAGE_JSON = 'package.json';
const PNPM_WORKSPACE = 'pnpm-workspace.yaml';

// The live .github/copilot-instructions.md is pulled from the spec repo into the
// instructions-test fixture so evals exercise the real authoring instructions
// instead of a checked-in copy. Resolved relative to this script, independent of
// the package-files DEST argument.
const COPILOT_INSTRUCTIONS_SRC = path.join('.github', 'copilot-instructions.md');
const COPILOT_INSTRUCTIONS_DEST = path.resolve(
    __dirname, '..', 'fixtures', 'instructions-test', 'copilot-instructions.md');

const DEST = process.argv[2]
    ? path.resolve(process.cwd(), process.argv[2])
    : path.resolve(__dirname, '..', 'fixtures', 'Microsoft.Widget', 'Widget');

function run(cmd, args, opts = {}) {
    console.log(`> ${cmd} ${args.join(' ')}`);
    execFileSync(cmd, args, { stdio: 'inherit', ...opts });
}

function parseYamlScalar(value) {
    const trimmed = value.trim();
    if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
        return JSON.parse(trimmed);
    }
    if (trimmed.startsWith("'") && trimmed.endsWith("'")) {
        return trimmed.slice(1, -1).replace(/''/g, "'");
    }
    return trimmed;
}

function parseFlatYamlSection(content, sectionName) {
    const lines = content.split(/\r?\n/);
    const start = lines.findIndex((line) => line === `${sectionName}:`);
    if (start === -1) {
        throw new Error(`Expected ${sectionName}: section in ${PNPM_WORKSPACE}`);
    }

    const result = {};
    for (const line of lines.slice(start + 1)) {
        if (line && !line.startsWith(' ')) break;

        const match = line.match(/^  (.+?):\s+(.+?)\s*(?:#.*)?$/);
        if (!match) continue;
        result[parseYamlScalar(match[1])] = parseYamlScalar(match[2]);
    }
    return result;
}

function createStandalonePackage(tmp) {
    const packagePath = path.join(tmp, PACKAGE_JSON);
    const workspacePath = path.join(tmp, PNPM_WORKSPACE);
    for (const filePath of [packagePath, workspacePath]) {
        if (!fs.existsSync(filePath)) {
            throw new Error(`Expected file not present after sparse checkout: ${filePath}`);
        }
    }

    const pkg = JSON.parse(fs.readFileSync(packagePath, 'utf8'));
    const workspace = fs.readFileSync(workspacePath, 'utf8');
    const catalog = parseFlatYamlSection(workspace, 'catalog');

    delete pkg.packageManager;
    delete pkg.workspaces;
    for (const depKey of ['dependencies', 'devDependencies', 'optionalDependencies']) {
        if (!pkg[depKey]) continue;
        for (const [name, version] of Object.entries(pkg[depKey])) {
            if (version === 'catalog:') {
                if (!catalog[name]) {
                    throw new Error(`No catalog version found for ${name}`);
                }
                pkg[depKey][name] = catalog[name];
            } else if (typeof version === 'string' && version.startsWith('workspace:')) {
                delete pkg[depKey][name];
            } else if (typeof version === 'string' && version.startsWith('catalog:')) {
                throw new Error(`Unsupported named catalog reference for ${name}: ${version}`);
            }
        }
    }
    pkg.overrides = parseFlatYamlSection(workspace, 'overrides');
    return pkg;
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
    const pkgPath = path.join(DEST, PACKAGE_JSON);
    fs.writeFileSync(pkgPath, JSON.stringify(createStandalonePackage(tmp), null, 2) + '\n');
    console.log(`created standalone ${PACKAGE_JSON} -> ${pkgPath}`);

    // Copy the live .github/copilot-instructions.md into the instructions-test fixture.
    const ciSrc = path.join(tmp, COPILOT_INSTRUCTIONS_SRC);
    if (!fs.existsSync(ciSrc)) {
        throw new Error(`Expected file not present after sparse checkout: ${ciSrc}`);
    }
    fs.mkdirSync(path.dirname(COPILOT_INSTRUCTIONS_DEST), { recursive: true });
    fs.copyFileSync(ciSrc, COPILOT_INSTRUCTIONS_DEST);
    console.log(`copied ${COPILOT_INSTRUCTIONS_SRC} -> ${COPILOT_INSTRUCTIONS_DEST}`);

    // Generate an npm lockfile because eval work directories use npm ci and do
    // not contain the source repository's pnpm workspace.
    const lockPath = path.join(DEST, 'package-lock.json');
    fs.rmSync(lockPath, { force: true });
    const npmCommand = process.platform === 'win32' ? process.execPath : 'npm';
    const npmArgs = process.platform === 'win32'
        ? [path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js')]
        : [];
    run(npmCommand, [
        ...npmArgs,
        'install',
        '--package-lock-only',
        '--ignore-scripts',
        '--no-audit',
        '--no-fund',
        '--workspaces=false',
        '--legacy-peer-deps',
    ], { cwd: DEST });
    if (!fs.existsSync(lockPath)) {
        throw new Error(`npm did not create expected lockfile: ${lockPath}`);
    }
    console.log(`generated package-lock.json -> ${lockPath}`);
} finally {
    fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 5 });
}
