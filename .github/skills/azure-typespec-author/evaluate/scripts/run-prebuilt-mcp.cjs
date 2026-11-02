#!/usr/bin/env node

const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const mcpKind = process.env.AZSDK_EVAL_MCP_KIND;
const repoRoot = process.env.AZSDK_EVAL_REPO_ROOT || process.cwd();
if (!['live', 'mock'].includes(mcpKind) || !repoRoot) {
    console.error('MCP kind must be live or mock, and the repository root must be available.');
    process.exit(1);
}

const serverCommand = mcpKind === 'live'
    ? ['artifacts/mcp/cli/azsdk.dll', 'mcp']
    : ['artifacts/mcp/mock/azsdk-mock.dll'];
const [relativeDllPath, ...serverArgs] = serverCommand;
const dllPath = path.resolve(repoRoot, relativeDllPath);
if (!fs.existsSync(dllPath)) {
    console.error(`Prebuilt ${mcpKind} MCP binary does not exist: ${dllPath}`);
    process.exit(1);
}

const child = spawn('dotnet', [dllPath, ...serverArgs], {
    stdio: 'inherit',
    env: process.env,
});

child.on('error', (error) => {
    console.error(error.message);
    process.exit(1);
});

child.on('exit', (code, signal) => {
    if (signal) {
        process.kill(process.pid, signal);
        return;
    }

    process.exit(code ?? 1);
});