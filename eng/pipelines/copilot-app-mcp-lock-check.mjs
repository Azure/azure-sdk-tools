// Diagnostic only: verify a generated lock is runtime-only before evaluating it.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const relative = 'eng/common/scripts/eval/package-lock.json';
const before = JSON.parse(execFileSync('git', ['show', `HEAD:${relative}`], { cwd: source, encoding: 'utf8' }));
const after = JSON.parse(await readFile(path.join(source, relative), 'utf8'));
const keys = new Set([...Object.keys(before.packages), ...Object.keys(after.packages)]);
const changed = [...keys].filter((name) => JSON.stringify(before.packages[name]) !== JSON.stringify(after.packages[name]));
assert.deepEqual(after.packages[''], before.packages['']);
assert.ok(changed.every((name) => name.startsWith('node_modules/@github/copilot')));
assert.equal(after.packages['node_modules/@microsoft/vally-cli'].version, '0.14.0');
assert.equal(after.packages['node_modules/@microsoft/vally'].version, '0.14.0');
assert.equal(after.packages['node_modules/@github/copilot-sdk'].version, '1.0.11');
assert.equal(after.packages['node_modules/@github/copilot'].version, '1.0.101');
const platforms = Object.entries(after.packages['node_modules/@github/copilot'].optionalDependencies).map(([name, version]) => ({
    name, version, lockedVersion: after.packages[`node_modules/${name}`]?.version,
}));
const output = path.join(source, 'artifacts/mcp-controls');
await mkdir(output, { recursive: true });
const summary = { changed, platforms, missingPlatformMetadata: platforms.filter(({ lockedVersion }) => !lockedVersion).map(({ name }) => name) };
await copyFile(path.join(source, relative), path.join(output, 'runtime-package-lock.json'));
await writeFile(path.join(output, 'lock-check.json'), JSON.stringify(summary, null, 2));
console.log(`RUNTIME_LOCK_CHECK ${JSON.stringify(summary)}`);