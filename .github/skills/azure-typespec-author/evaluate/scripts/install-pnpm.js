#!/usr/bin/env node
/*
 * Install the pnpm version pinned by the sparse-cloned azure-rest-api-specs
 * package.json. Run setup-fixture-files.js first to prepare the clone.
 */

const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const REPO_ROOT = path.resolve(__dirname, '..', '..', '..', '..', '..');
const SPEC_PACKAGE = path.join(REPO_ROOT, 'artifacts', 'azure-rest-api-specs', 'package.json');

function run(command, args, capture = false) {
    const result = spawnSync(command, args, {
        encoding: capture ? 'utf8' : undefined,
        stdio: capture ? ['ignore', 'pipe', 'ignore'] : 'inherit',
        shell: process.platform === 'win32',
    });
    if (result.error) throw result.error;
    return {
        code: result.status ?? 1,
        stdout: capture ? result.stdout.trim() : '',
    };
}

if (!fs.existsSync(SPEC_PACKAGE)) {
    throw new Error(
        `Spec repository package not found: ${SPEC_PACKAGE}. Run setup-fixture-files.js first.`);
}

const pkg = JSON.parse(fs.readFileSync(SPEC_PACKAGE, 'utf8'));
const match = /^pnpm@([^+]+)(?:\+.*)?$/.exec(pkg.packageManager || '');
if (!match) {
    throw new Error(`Expected pnpm packageManager in ${SPEC_PACKAGE}, got: ${pkg.packageManager}`);
}

const version = match[1];
const installed = run('pnpm', ['--version'], true);
if (installed.code === 0 && installed.stdout === version) {
    console.log(`pnpm@${version} is already installed. Nothing to do.`);
    process.exit(0);
}

const args = ['install', '--global', `pnpm@${version}`];
console.log(`> npm ${args.join(' ')}`);
const result = run('npm', args);
if (result.code !== 0) {
    throw new Error(`npm ${args.join(' ')} failed with exit code ${result.code}`);
}
console.log(`Installed pnpm@${version}.`);
