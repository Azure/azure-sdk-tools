// Diagnostic only: measure real wall-clock expiry with the production dependency stack.
// No token, session transcript, credential home, or raw error is written to artifacts.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const COMMON = path.join(ROOT, 'eng/common/scripts/eval');
const OUTPUT = path.join(ROOT, 'artifacts/token-lifetime');
const MINUTE = 60_000;
const MARKER = 'TOKEN_LIFETIME_OK';
export const CHECKPOINT_MINUTES = Object.freeze([30, 55, 65]);
const AUTH_KEYS = [
    'COPILOT_GITHUB_TOKEN', 'GH_TOKEN', 'GITHUB_TOKEN', 'GH_ENTERPRISE_TOKEN',
    'GITHUB_ENTERPRISE_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'COPILOT_SDK_AUTH_TOKEN',
    'CAPI_HMAC_KEY', 'COPILOT_HMAC_KEY', 'COPILOT_API_URL', 'COPILOT_CLI_PATH',
    'COPILOT_HOME', 'GH_CONFIG_DIR', 'XDG_CONFIG_HOME', 'EVALUATE_USE_HOST_COPILOT_HOME',
    'COPILOT_SDK_DEFAULT_CONNECTION',
];

export function isolatedEnvironment(source, token, home) {
    const env = { ...source };
    for (const key of AUTH_KEYS) delete env[key];
    return { ...env, COPILOT_GITHUB_TOKEN: token, COPILOT_HOME: home,
        GH_CONFIG_DIR: path.join(home, 'gh'), XDG_CONFIG_HOME: path.join(home, 'xdg') };
}

export function errorCategory(error) {
    const text = String(error?.message ?? error);
    if (/401|403|authoriz|authenticat|credential|token.*expir/i.test(text)) return 'authentication';
    if (/timeout|timed out|deadline/i.test(text)) return 'timeout';
    if (/model.*(unavailable|not found)|unknown model/i.test(text)) return 'model-unavailable';
    if (/connection|disconnect|disposed|closed/i.test(text)) return 'connection';
    return 'other';
}

export function checkpointDelay(started, minute, now = Date.now()) {
    return Math.max(0, started + minute * MINUTE - now);
}

function save(name, value) {
    mkdirSync(OUTPUT, { recursive: true });
    writeFileSync(path.join(OUTPUT, name), JSON.stringify(value, null, 2));
}

async function bounded(operation, milliseconds = 120_000) {
    let timer;
    try {
        return await Promise.race([operation(), new Promise((_, reject) => {
            timer = setTimeout(() => reject(new Error('Diagnostic operation timed out')), milliseconds);
        })]);
    } finally { clearTimeout(timer); }
}

export async function installationStatus(token, request = fetch) {
    try {
        const response = await request('https://api.github.com/installation/repositories?per_page=1', {
            headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json',
                'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'azure-sdk-token-lifetime-diagnostic' },
            redirect: 'error', signal: AbortSignal.timeout(30_000),
        });
        const status = response.status;
        if (!response.ok) {
            await response.body?.cancel();
            return { status, authenticated: false };
        }
        const body = await response.json();
        const scoped = body.total_count === 1 && body.repositories?.[0]?.full_name === 'Azure/azure-sdk-tools';
        return { status, authenticated: scoped, repositoryScopeCorrect: scoped };
    } catch (error) { return { status: null, authenticated: false, error: errorCategory(error) }; }
}

function latestJsonl(directory) {
    const candidates = [];
    const visit = folder => {
        for (const entry of readdirSync(folder, { withFileTypes: true })) {
            const target = path.join(folder, entry.name);
            if (entry.isDirectory()) visit(target);
            else if (entry.name === 'results.jsonl') candidates.push(target);
        }
    };
    visit(directory);
    assert.equal(candidates.length, 1, 'Expected one diagnostic Vally result');
    return readFileSync(candidates[0], 'utf8').split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line));
}

function vallyControl(token, home) {
    const output = path.join(home, 'vally-results');
    const proc = spawnSync(process.execPath, [path.join(COMMON, 'invoke-eval-shard.ts'),
        '--eval-args', '-e ../eng/pipelines/copilot-token-lifetime.eval.yaml',
        '--shard-name', 'token-lifetime', '--output-dir', output, '--threshold', '1'], {
        cwd: path.join(ROOT, 'evals'), env: isolatedEnvironment(process.env, token, home),
        encoding: 'utf8', timeout: 240_000, maxBuffer: 8 * 1024 * 1024,
    });
    try {
        const records = latestJsonl(output);
        const trials = records.filter(record => record.type === 'trial-result');
        const summary = records.filter(record => record.type === 'run-summary').at(-1);
        return { processExit: proc.status, complete: !!summary && trials.length === 2,
            trials: trials.map(trial => ({ stimulus: trial.stimulus, status: trial.status,
                passed: trial.gradeResult?.passed === true,
                ...(trial.error ? { error: errorCategory(trial.error) } : {}) })),
            passed: !!summary && trials.length === 2 && trials.every(trial =>
                trial.status !== 'error' && trial.gradeResult?.passed === true) };
    } catch { return { processExit: proc.status, complete: false, passed: false,
        error: errorCategory(proc.error ?? (proc.stderr + proc.stdout)) }; }
}

async function openSdk(token, home, CopilotClient) {
    const client = new CopilotClient({ env: isolatedEnvironment(process.env, token, home),
        workingDirectory: home });
    const handle = { client, home };
    try {
        handle.session = await newSession(handle);
        return handle;
    } catch (error) { await closeSdk(handle); throw error; }
}

function newSession(handle) {
    return bounded(() => handle.client.createSession({ model: 'gpt-6.1-sol',
        workingDirectory: handle.home, configDirectory: handle.home,
        skillDirectories: [], availableTools: [], streaming: false,
        infiniteSessions: { enabled: false },
        onPermissionRequest: async () => ({ kind: 'denied-no-approval-rule' }),
    }));
}

async function prompt(session) {
    try {
        const response = await bounded(() => session.sendAndWait({
            prompt: `Reply with exactly ${MARKER}. Do not use any tools.`,
        }, 90_000));
        return { passed: response?.data?.content?.trim() === MARKER };
    } catch (error) { return { passed: false, error: errorCategory(error) }; }
}

async function closeSdk(handle) {
    if (!handle) return;
    try { await bounded(() => handle.session?.disconnect(), 10_000); } catch { /* Diagnostic cleanup only. */ }
    try { await bounded(() => handle.client.stop(), 15_000); }
    catch { try { await bounded(() => handle.client.forceStop(), 10_000); } catch { /* Job cleanup is final. */ } }
}

async function freshSdk(token, home, CopilotClient) {
    let handle;
    try { handle = await openSdk(token, home, CopilotClient); return await prompt(handle.session); }
    catch (error) { return { passed: false, error: errorCategory(error) }; }
    finally { await closeSdk(handle); }
}

export function conclusions(original, control) {
    const final = original.checkpoints?.find(checkpoint => checkpoint.minute === 65);
    return {
        realExpiryBoundaryObserved: !!final && final.elapsedMinutes >= 65,
        originalTokenHttpStatus: final?.installation.status ?? null,
        originalInstallationTokenRejected: final?.installation.status === 401,
        freshInstallationTokenAccepted: control.installation.authenticated === true,
        sameSessionAfterBoundary: final?.sameSession.passed ?? false,
        newSessionOnSameClientAfterBoundary: final?.newSession?.passed ?? false,
        freshSdkWithOriginalTokenAfterBoundary: final?.freshSdk?.passed ?? false,
        normalVallyWithOriginalTokenAfterBoundary: final?.vally?.passed ?? false,
        normalVallyWithNewToken: control.vally.passed,
        automaticAppTokenRenewalImplemented: false,
        note: 'Cached Copilot success is not evidence that the GitHub installation token was renewed.',
    };
}

async function main(phase) {
    assert.ok(['original', 'fresh-control'].includes(phase), 'Specify --phase original|fresh-control');
    const token = process.env.COPILOT_GITHUB_TOKEN;
    assert.ok(token?.startsWith('ghs_'), 'Shared login must provide an installation token');
    const sdkRoot = path.join(COMMON, 'node_modules/@github/copilot-sdk');
    const sdk = JSON.parse(readFileSync(path.join(sdkRoot, 'package.json'), 'utf8'));
    assert.equal(sdk.version, '1.0.15', 'Test the production SDK, not a replacement');
    const { CopilotClient } = await import(pathToFileURL(path.join(sdkRoot, 'dist/index.js')).href);
    const vally = JSON.parse(readFileSync(path.join(COMMON, 'node_modules/@microsoft/vally/package.json'), 'utf8'));
    assert.equal(vally.version, '0.19.0');
    const scratch = mkdtempSync(path.join(tmpdir(), 'copilot-token-lifetime-'));
    const home = name => { const dir = path.join(scratch, name); mkdirSync(dir); return dir; };
    let handle;
    try {
        if (phase === 'fresh-control') {
            const original = JSON.parse(readFileSync(path.join(OUTPUT, 'original.json'), 'utf8'));
            assert.equal(original.completed, true, 'Original-token phase did not complete');
            const control = { utc: new Date().toISOString(), installation: await installationStatus(token),
                sdk: await freshSdk(token, home('fresh-sdk'), CopilotClient),
                vally: vallyControl(token, home('fresh-vally')) };
            const summary = conclusions(original, control);
            save('fresh-control.json', control); save('summary.json', summary);
            console.log('TOKEN_LIFETIME_SUMMARY ' + JSON.stringify(summary));
            assert.ok(summary.realExpiryBoundaryObserved && [200, 401].includes(summary.originalTokenHttpStatus) && control.installation.authenticated &&
                control.sdk.passed && control.vally.passed, 'Recovery control failed; evidence is inconclusive');
            return;
        }
        const started = Date.now();
        const report = { startedAt: new Date(started).toISOString(), versions: { sdk: sdk.version, vally: vally.version },
            checkpoints: [], completed: false, automaticRenewalConfigured: false };
        save('original.json', report);
        report.initialInstallation = await installationStatus(token);
        save('original.json', report);
        assert.equal(report.initialInstallation.authenticated, true, 'Initial installation scope/authentication failed');
        try { handle = await openSdk(token, home('persistent-sdk'), CopilotClient); }
        catch (error) {
            report.initialSdk = { passed: false, error: errorCategory(error) };
            save('original.json', report);
            throw error;
        }
        report.initialSdk = await prompt(handle.session);
        report.initialVally = vallyControl(token, home('initial-vally'));
        save('original.json', report);
        console.log('TOKEN_LIFETIME_INITIAL ' + JSON.stringify({ utc: report.startedAt,
            installation: report.initialInstallation, sdk: report.initialSdk, vally: report.initialVally }));
        assert.ok(report.initialSdk.passed && report.initialVally.passed, 'Initial model/judge control failed');
        for (const minute of CHECKPOINT_MINUTES) {
            // Real elapsed time is the test input. This is a fixed expiry experiment, not build polling.
            await new Promise(resolve => setTimeout(resolve, checkpointDelay(started, minute)));
            const checkpoint = { minute, utc: new Date().toISOString(), elapsedMinutes: (Date.now() - started) / MINUTE,
                installation: await installationStatus(token), sameSession: await prompt(handle.session) };
            if (minute === 65) {
                let session;
                try { session = await newSession(handle); checkpoint.newSession = await prompt(session); }
                catch (error) { checkpoint.newSession = { passed: false, error: errorCategory(error) }; }
                finally { try { await bounded(() => session?.disconnect(), 10_000); } catch { /* Cleanup only. */ } }
                checkpoint.freshSdk = await freshSdk(token, home('original-token-fresh-sdk'), CopilotClient);
                checkpoint.vally = vallyControl(token, home('original-token-fresh-vally'));
            }
            report.checkpoints.push(checkpoint);
            save('original.json', report);
            console.log('TOKEN_LIFETIME_CHECKPOINT ' + JSON.stringify(checkpoint));
        }
        report.completed = true;
        report.finishedAt = new Date().toISOString();
        report.originalEnvironmentValueUnchanged = process.env.COPILOT_GITHUB_TOKEN === token;
        save('original.json', report);
    } finally { await closeSdk(handle); rmSync(scratch, { recursive: true, force: true }); }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main(process.argv[process.argv.indexOf('--phase') + 1]).catch(error => {
        // Deliberately never print native error text or a stack containing credentials.
        console.error('Token lifetime diagnostic stopped: ' + errorCategory(error));
        process.exitCode = 1;
    });
}
