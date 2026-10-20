import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { optionCases, startConfiguredMock } from './copilot-app-mcp-options.mjs';

test('matrix distinguishes native session-fs, auth auto-detection and token name', () => {
    const cases = optionCases();
    assert.equal(new Set(cases.map(({ name }) => name)).size, 8);
    const control = cases.find(({ name }) => name === 'vally-native-fs-control');
    const noFs = cases.find(({ name }) => name === 'vally-without-native-fs');
    assert.deepEqual({ ...control, name: noFs.name, nativeFs: false }, noFs);
    const direct = cases.filter(({ direct }) => direct);
    assert.deepEqual(direct.map(({ nativeFs, useLoggedInUser }) => [nativeFs, useLoggedInUser]), [[false, true], [false, false], [true, true], [true, false]]);
    assert.equal(cases.filter(({ explicitStart }) => explicitStart).length, 1);
    assert.equal(cases.filter(({ tokenName }) => tokenName).length, 1);
});

test('ephemeral start respects disabled, filtered and third-party-MCP policy', async () => {
    let called = false;
    const session = { rpc: { mcp: { startServer: async () => { called = true; } } } };
    const config = { 'azure-sdk-mcp': { command: 'dotnet', args: ['../artifacts/mcp/mock/azsdk-mock.dll'], cwd: path.resolve('evals') } };
    for (const state of [{ mcp3pEnabled: false }, { mcp3pEnabled: true, disabledServers: ['azure-sdk-mcp'] }, { mcp3pEnabled: true, filteredServers: ['azure-sdk-mcp'] }]) {
        await assert.rejects(startConfiguredMock(session, config, state));
    }
    assert.equal(called, false);
});

test('ephemeral start reuses only the exact configured mock without persistent config writes', async () => {
    const calls = [];
    const session = { rpc: { mcp: { startServer: async (params) => calls.push(params) } } };
    const server = { command: 'dotnet', args: ['../artifacts/mcp/mock/azsdk-mock.dll'], cwd: path.resolve('evals'), tools: ['*'] };
    await startConfiguredMock(session, { 'azure-sdk-mcp': server }, { mcp3pEnabled: true, disabledServers: [], filteredServers: [] });
    assert.deepEqual(calls, [{ name: 'azure-sdk-mcp', config: server }]);
    await assert.rejects(startConfiguredMock(session, { 'azure-sdk-mcp': { ...server, args: ['azsdk.dll', 'start'] } }, { mcp3pEnabled: true }));
    assert.equal(calls.length, 1);
});