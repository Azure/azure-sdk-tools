import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { CHECKPOINT_MINUTES, checkpointDelay, conclusions, errorCategory,
    installationStatus, isolatedEnvironment } from './copilot-token-lifetime.mjs';

test('uses real checkpoints beyond the one-hour lifetime', () => {
    assert.deepEqual(CHECKPOINT_MINUTES, [30, 55, 65]);
    assert.equal(checkpointDelay(1000, 65, 1000), 65 * 60_000);
    assert.equal(checkpointDelay(1000, 65, 1000 + 66 * 60_000), 0);
});

test('retains only the intended token and isolates saved credential locations', () => {
    const env = isolatedEnvironment({ PATH: 'retained', GH_TOKEN: 'old', GITHUB_TOKEN: 'old',
        COPILOT_GITHUB_TOKEN: 'old', GITHUB_COPILOT_API_TOKEN: 'old', COPILOT_SDK_AUTH_TOKEN: 'old',
        COPILOT_CLI_PATH: 'custom-runtime', EVALUATE_USE_HOST_COPILOT_HOME: 'true' }, 'intended', '/isolated');
    assert.equal(env.PATH, 'retained');
    assert.equal(env.COPILOT_GITHUB_TOKEN, 'intended');
    assert.equal(env.COPILOT_HOME, '/isolated');
    for (const key of ['GH_TOKEN', 'GITHUB_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'COPILOT_SDK_AUTH_TOKEN',
        'COPILOT_CLI_PATH', 'EVALUATE_USE_HOST_COPILOT_HOME']) assert.equal(env[key], undefined);
});

test('installation probe uses a fixed authenticated endpoint and does not disclose token/body', async () => {
    const result = await installationStatus('never-disclose', async (url, options) => {
        assert.equal(url, 'https://api.github.com/installation/repositories?per_page=1');
        assert.equal(options.headers.Authorization, 'Bearer never-disclose');
        assert.equal(options.redirect, 'error');
        return { status: 200, ok: true, json: async () => ({ total_count: 1,
            repositories: [{ full_name: 'Azure/azure-sdk-tools', private: 'never-export' }] }) };
    });
    assert.deepEqual(result, { status: 200, authenticated: true, repositoryScopeCorrect: true });
    assert.doesNotMatch(JSON.stringify(result), /never-disclose|never-export/);
});

test('expired installation token is recorded as an observation, not an SDK assumption', async () => {
    const result = await installationStatus('expired', async () => ({ status: 401, ok: false,
        body: { cancel: async () => {} } }));
    assert.deepEqual(result, { status: 401, authenticated: false });
});

test('broad repository access does not satisfy the scope control', async () => {
    const result = await installationStatus('scoped', async () => ({ status: 200, ok: true,
        json: async () => ({ total_count: 40, repositories: [{ full_name: 'Azure/azure-sdk-tools' }] }) }));
    assert.equal(result.authenticated, false);
});

test('cached model success is not labeled App-token renewal', () => {
    const result = conclusions({ checkpoints: [{ minute: 65, elapsedMinutes: 65.1,
        installation: { status: 401 }, sameSession: { passed: true }, newSession: { passed: true },
        freshSdk: { passed: false }, vally: { passed: false } }] }, {
        installation: { authenticated: true }, vally: { passed: true } });
    assert.equal(result.originalInstallationTokenRejected, true);
    assert.equal(result.sameSessionAfterBoundary, true);
    assert.equal(result.freshSdkWithOriginalTokenAfterBoundary, false);
    assert.equal(result.automaticAppTokenRenewalImplemented, false);
});

test('error categories cannot leak raw native exception details', () => {
    assert.equal(errorCategory(new Error('401 token=never-disclose')), 'authentication');
    assert.equal(errorCategory(new Error('connection disposed')), 'connection');
    assert.equal(errorCategory(new Error('timed out')), 'timeout');
    assert.equal(errorCategory(new Error('some secret detail')), 'other');
});

test('diagnostic YAML reuses the existing scoped login without production write scenarios', () => {
    const pipeline = readFileSync(new URL('./copilot-app-vally-eval-test.yml', import.meta.url), 'utf8');
    assert.equal((pipeline.match(/template: \/eng\/common\/pipelines\/templates\/steps\/login-to-copilot.yml/g) ?? []).length, 2);
    assert.match(pipeline, /timeoutInMinutes: 100/);
    assert.doesNotMatch(pipeline, /PAT|azureSubscription:|UseAzSdkAuthentication|vally-results-dashboard/);
});