// Diagnostic only: verify a generated lock updates only the matching SDK/runtime closure.
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
const allowed = [
    'node_modules/@github/copilot', 'node_modules/@koromix/koffi-', 'node_modules/koffi',
    'node_modules/zod', 'node_modules/detect-libc',
];
assert.ok(changed.every((name) => allowed.some((prefix) => name.startsWith(prefix))), 'Unrelated dependency changed');
assert.equal(after.packages['node_modules/@microsoft/vally-cli'].version, '0.14.0');
assert.equal(after.packages['node_modules/@microsoft/vally'].version, '0.14.0');
assert.equal(after.packages['node_modules/@github/copilot-sdk'].version, '1.0.24');
const platforms = Object.entries(after.packages['node_modules/@github/copilot-sdk'].optionalDependencies).map(([name, version]) => ({
    name, version, lockedVersion: after.packages[`node_modules/${name}`]?.version,
}));
const output = path.join(source, 'artifacts/mcp-controls');
await mkdir(output, { recursive: true });
const summary = { changed, platforms, missingPlatformMetadata: platforms.filter(({ lockedVersion }) => !lockedVersion).map(({ name }) => name) };
assert.equal(platforms.length, 8);
assert.equal(summary.missingPlatformMetadata.length, 0, 'Platform metadata must not be silently dropped');
assert.ok(platforms.every(({ version, lockedVersion }) => version === '1.0.24' && lockedVersion === version));
await copyFile(path.join(source, relative), path.join(output, 'runtime-package-lock.json'));
await writeFile(path.join(output, 'lock-check.json'), JSON.stringify(summary, null, 2));
console.log(`RUNTIME_LOCK_CHECK ${JSON.stringify(summary)}`);