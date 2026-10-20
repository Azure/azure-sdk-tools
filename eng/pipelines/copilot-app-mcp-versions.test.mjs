import assert from 'node:assert/strict';
import test from 'node:test';
import { selectDiagnostics, versionCases } from './copilot-app-mcp-versions.mjs';

test('version matrix separates runtime-only change, SDK change, and documented auto-login flag', () => {
    const cases = versionCases();
    assert.equal(cases.length, 4);
    assert.deepEqual(cases.map(({ sdk, runtime, autoLogin }) => [sdk, runtime, autoLogin]), [
        ['pinned', 'pinned', true], ['pinned', 'stable', true], ['stable', 'stable', true], ['stable', 'stable', false],
    ]);
});

test('only bounded and sanitized MCP diagnostics are publishable', () => {
    const lines = selectDiagnostics([
        'Received session.create request: {"authorization":"secret"}',
        'MCP server "example" filtered: registry policy',
        'Failed to initialize MCP host: secret-value',
        'MCP server failed: Authorization: Bearer secret-value',
        'MCP server failed: token=secret-value',
        'Other log output',
    ].join('\n'), 'secret-value');
    assert.deepEqual(lines, ['MCP server "example" filtered: registry policy', 'Failed to initialize MCP host: [REDACTED]']);
    assert.equal(selectDiagnostics(Array(50).fill('MCP server "x" filtered: no').join('\n'), 'unused').length, 20);
});