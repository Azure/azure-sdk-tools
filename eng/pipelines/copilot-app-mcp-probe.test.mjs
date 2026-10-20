import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { buildCases, inspectMcp, redact, summarizeTrajectory } from './copilot-app-mcp-probe.mjs';

test('path control changes only cwd and does not mutate the configured server', () => {
    const servers = { 'azure-sdk-mcp': { type: 'stdio', command: 'dotnet', cwd: '.', args: ['../artifacts/mcp/mock/azsdk-mock.dll'], timeout: '30s' } };
    const before = structuredClone(servers);
    const cases = buildCases(servers, path.resolve('evals'));
    assert.equal(cases.length, 3);
    assert.equal(cases[0].servers, undefined);
    assert.deepEqual(cases[1].servers, before);
    assert.deepEqual(cases[2].servers, { 'azure-sdk-mcp': { ...before['azure-sdk-mcp'], cwd: path.resolve('evals') } });
    assert.deepEqual(servers, before);
});

test('does not silently probe a live server', () => {
    assert.throws(() => buildCases({ 'azure-sdk-mcp': { command: 'dotnet', args: ['../artifacts/mcp/cli/azsdk.dll', 'start'] } }, '/repo/evals'));
});

test('MCP policy and actual live catalog are recorded independently', async () => {
    const result = await inspectMcp({ rpc: { mcp: {
        list: async () => ({ servers: [{ name: 'azure-sdk-mcp', status: 'connected' }], host: { mcp3pEnabled: true, clients: ['azure-sdk-mcp'] } }),
        listTools: async ({ serverName }) => { assert.equal(serverName, 'azure-sdk-mcp'); return { tools: [{ name: 'azsdk_get_github_user_details' }] }; },
    } } });
    assert.equal(result.mcp3pEnabled, true);
    assert.equal(result.requiredToolAvailable, true);
    assert.equal(result.toolCount, 1);
});

test('a disabled or failed server is not counted as available', async () => {
    const result = await inspectMcp({ rpc: { mcp: {
        list: async () => ({ servers: [], host: { mcp3pEnabled: false } }),
        listTools: async () => { throw new Error('not connected'); },
    } } });
    assert.equal(result.mcp3pEnabled, false);
    assert.equal(result.requiredToolAvailable, false);
    assert.equal(result.listToolsError, 'not connected');
});

test('response prose is not accepted as evidence of a successful tool call', () => {
    const trajectory = { events: [], metadata: { model: 'test' }, metrics: {}, output: 'I called azsdk_get_github_user_details successfully.' };
    assert.equal(summarizeTrajectory(trajectory).requiredToolSucceeded, false);
    trajectory.events = [
        { type: 'tool_call', data: { toolName: 'azure-sdk-mcp-azsdk_get_github_user_details', toolCallId: '1' } },
        { type: 'tool_result', data: { toolName: 'azure-sdk-mcp-azsdk_get_github_user_details', toolCallId: '1', success: false } },
    ];
    assert.equal(summarizeTrajectory(trajectory).requiredToolSucceeded, false);
    trajectory.events[1].data.success = true;
    assert.equal(summarizeTrajectory(trajectory).requiredToolSucceeded, true);
});

test('credentials and pipeline logging commands are sanitized', () => {
    assert.equal(redact('secret-value ghs_other123 ##vso[x]', 'secret-value'), '[REDACTED] [REDACTED] [diagnostic-vso x]');
});