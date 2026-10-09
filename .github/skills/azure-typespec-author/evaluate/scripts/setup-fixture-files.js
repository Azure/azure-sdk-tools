#!/usr/bin/env node
/*
 * Sparse-clone azure-rest-api-specs without specification/ and copy its
 * Copilot instructions into the local fixture.
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
const COPILOT_INSTRUCTIONS_RELATIVE = path.join('.github', 'copilot-instructions.md');

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

const fixturePackage = path.join(FIXTURE_ROOT, 'Microsoft.Widget', 'Widget', 'package.json');
if (!fs.existsSync(fixturePackage)) {
    throw new Error(`Required fixture package does not exist: ${fixturePackage}`);
}

const instructionsSource = path.join(SPEC_REPO_ROOT, COPILOT_INSTRUCTIONS_RELATIVE);
const instructionsDestination = path.join(
    FIXTURE_ROOT, 'instructions-test', 'copilot-instructions.md');
if (!fs.existsSync(instructionsSource)) {
    throw new Error(`Required fixture input does not exist: ${instructionsSource}`);
}
fs.mkdirSync(path.dirname(instructionsDestination), { recursive: true });
fs.copyFileSync(instructionsSource, instructionsDestination);
console.log(`copied ${instructionsSource} -> ${instructionsDestination}`);
