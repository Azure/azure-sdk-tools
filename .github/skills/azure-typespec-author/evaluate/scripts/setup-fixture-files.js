#!/usr/bin/env node
/*
 * Sparse-clone azure-rest-api-specs without specification/ and copy its
 * Copilot instructions and generated package metadata into the local fixture.
 *
 * Dependencies are installed separately from the clone's native pnpm workspace
 * by install-pnpm.js and setup-environment.js.
 */

const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const REPO_URL = 'https://github.com/Azure/azure-rest-api-specs.git';
const BRANCH = 'main';
const REPO_ROOT = path.resolve(__dirname, '..', '..', '..', '..', '..');
const SPEC_REPO_ROOT = path.join(REPO_ROOT, 'artifacts', 'azure-rest-api-specs');
const FIXTURE_ROOT = path.resolve(__dirname, '..', 'fixtures');
const GENERATED_FIXTURE_ROOT = path.join(FIXTURE_ROOT, 'Microsoft.Widget', 'Widget');
const COPILOT_INSTRUCTIONS_RELATIVE = path.join('.github', 'copilot-instructions.md');
// Keep the eval install minimal while sourcing every version from the spec repo's catalog.
const FIXTURE_DEPENDENCIES = [
    '@azure-tools/typespec-autorest',
    '@azure-tools/typespec-azure-core',
    '@azure-tools/typespec-azure-resource-manager',
    '@azure-tools/typespec-azure-rulesets',
    '@typespec/compiler',
    '@typespec/events',
    '@typespec/http',
    '@typespec/openapi',
    '@typespec/openapi3',
    '@typespec/rest',
    '@typespec/sse',
    '@typespec/streams',
    '@typespec/versioning',
];

function run(command, args) {
    console.log(`> ${command} ${args.join(' ')}`);
    execFileSync(command, args, { stdio: 'inherit' });
}

if (!fs.existsSync(path.join(SPEC_REPO_ROOT, '.git'))) {
    fs.mkdirSync(path.dirname(SPEC_REPO_ROOT), { recursive: true });
    run('git', [
        'clone',
        '--filter=blob:none',
        '--sparse',
        '--depth=1',
        '--branch', BRANCH,
        REPO_URL,
        SPEC_REPO_ROOT,
    ]);
    run('git', [
        '-C', SPEC_REPO_ROOT,
        'sparse-checkout', 'set', '--no-cone',
        '/*', '!/specification/',
    ]);
} else {
    run('git', ['-C', SPEC_REPO_ROOT, 'fetch', '--depth=1', 'origin', BRANCH]);
    run('git', ['-C', SPEC_REPO_ROOT, 'checkout', '--detach', 'FETCH_HEAD']);
}

const workspaceFile = path.join(SPEC_REPO_ROOT, 'pnpm-workspace.yaml');
if (!fs.existsSync(workspaceFile)) {
    throw new Error(`Required pnpm workspace file does not exist: ${workspaceFile}`);
}

const catalog = new Map();
let inCatalog = false;
// Avoid a YAML package dependency here because this script runs before fixture dependencies exist.
// The pnpm catalog is a single top-level mapping, so its indentation provides a bounded parser.
for (const line of fs.readFileSync(workspaceFile, 'utf8').split(/\r?\n/)) {
    if (line === 'catalog:') {
        inCatalog = true;
        continue;
    }
    if (inCatalog && /^\S/.test(line)) {
        break;
    }
    if (!inCatalog) {
        continue;
    }
    const match = line.match(/^  "([^"]+)":\s*(\S+)\s*$/);
    if (match) {
        catalog.set(match[1], match[2]);
    }
}

const devDependencies = {};
for (const dependency of FIXTURE_DEPENDENCIES) {
    const version = catalog.get(dependency);
    if (!version) {
        throw new Error(`Required dependency is missing from the pnpm catalog: ${dependency}`);
    }
    devDependencies[dependency] = version;
}

fs.mkdirSync(GENERATED_FIXTURE_ROOT, { recursive: true });
const fixturePackage = path.join(GENERATED_FIXTURE_ROOT, 'package.json');
// Vally maps this stable fixture path into each trial. Generate it instead of committing
// package metadata that would drift from azure-rest-api-specs.
fs.writeFileSync(fixturePackage, `${JSON.stringify({
    name: 'microsoft-widget-eval-fixture',
    private: true,
    type: 'module',
    devDependencies,
}, null, 2)}\n`);
console.log(`generated ${fixturePackage}`);

const instructionsSource = path.join(SPEC_REPO_ROOT, COPILOT_INSTRUCTIONS_RELATIVE);
const instructionsDestination = path.join(
    FIXTURE_ROOT, 'instructions-test', 'copilot-instructions.md');
if (!fs.existsSync(instructionsSource)) {
    throw new Error(`Required fixture input does not exist: ${instructionsSource}`);
}
fs.mkdirSync(path.dirname(instructionsDestination), { recursive: true });
fs.copyFileSync(instructionsSource, instructionsDestination);
console.log(`copied ${instructionsSource} -> ${instructionsDestination}`);
