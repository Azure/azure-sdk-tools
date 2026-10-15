// Test-only probes: expected authentication failures must not override the control verdict.
import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const token = process.env.COPILOT_GITHUB_TOKEN;
if (!token?.startsWith('ghs_')) throw new Error('Shared login did not supply an installation token');
const source = process.env.BUILD_SOURCESDIRECTORY;
const output = path.join(source, 'artifacts', 'auth-probes');
mkdirSync(output, { recursive: true });
const results = [];
for (const mode of ['GITHUB_TOKEN', 'COPILOT_GITHUB_TOKEN']) {
    const env = { ...process.env };
    for (const key of ['GH_TOKEN', 'GITHUB_TOKEN', 'COPILOT_GITHUB_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'CAPI_HMAC_KEY', 'COPILOT_HMAC_KEY']) delete env[key];
    env[mode] = token;
    console.log(`Authentication probe: ${mode} only (all alternate token names removed)`);
    const result = spawnSync(process.execPath, [
        '--experimental-strip-types', path.join(source, 'eng/common/scripts/eval/invoke-eval-shard.ts'),
        '--eval-args', process.env.EVAL_ARGS,
        '--shard-name', mode,
        '--output-dir', path.join(output, mode),
        '--threshold', '1',
    ], { env, encoding: 'utf8', timeout: 360000, maxBuffer: 16 * 1024 * 1024 });
    // Disable Azure log commands in expected-failure diagnostic output, and redact defensively.
    const log = `${result.stdout ?? ''}\n${result.stderr ?? ''}`
        .replaceAll(token, '[REDACTED]')
        .replaceAll('##vso[', '[diagnostic-vso ')
        .replaceAll('##[', '[diagnostic ');
    console.log(log);
    const verdict = { mode, exitCode: result.status, passed: result.status === 0, error: result.error?.code ?? null };
    results.push(verdict);
    console.log(`AUTH_PROBE_RESULT ${JSON.stringify(verdict)}`);
}
writeFileSync(path.join(output, 'summary.json'), JSON.stringify(results, null, 2));
console.log(`AUTH_PROBE_SUMMARY ${JSON.stringify(results)}`);
