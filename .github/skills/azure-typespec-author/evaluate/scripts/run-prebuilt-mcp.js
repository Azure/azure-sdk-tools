#!/usr/bin/env node

const { spawn } = require('node:child_process');
const path = require('node:path');

const mcpKind = process.env.AZSDK_EVAL_MCP_KIND;
if (!['live', 'mock'].includes(mcpKind)) {
    console.error('AZSDK_EVAL_MCP_KIND must be live or mock.');
    process.exit(1);
}

const serverCommand = mcpKind === 'live'
    ? ['artifacts/mcp/cli/azsdk.dll', 'start']
    : ['artifacts/mcp/mock/azsdk-mock.dll'];
const [relativeDllPath, ...serverArgs] = serverCommand;
const dllPath = path.resolve(process.cwd(), relativeDllPath);
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