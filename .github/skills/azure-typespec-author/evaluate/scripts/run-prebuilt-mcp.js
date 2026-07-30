#!/usr/bin/env node

const { spawn } = require('node:child_process');
const path = require('node:path');

const PREBUILT_SERVERS = {
    'azsdk-mcp': {
        live: ['artifacts/mcp/cli/azsdk.dll', 'start'],
        mock: ['artifacts/mcp/mock/azsdk-mock.dll'],
    },
};

const root = process.env.AZSDK_EVAL_REPO_ROOT;
if (!root) {
    console.error('AZSDK_EVAL_REPO_ROOT is required for prebuilt MCP environments.');
    process.exit(1);
}

function getArgsStart() {
    const invokedAsScript = process.argv[1] && path.resolve(process.argv[1]) === __filename;
    return invokedAsScript ? 2 : 1;
}

function resolveServerCommand(serverNameOrRelativeDllPath) {
    const server = PREBUILT_SERVERS[serverNameOrRelativeDllPath];
    if (!server) {
        return [serverNameOrRelativeDllPath];
    }

    const mcpKind = process.env.AZSDK_EVAL_MCP_KIND || 'live';
    const serverCommand = server[mcpKind];
    if (!serverCommand) {
        console.error(`Unknown prebuilt MCP kind '${mcpKind}' for '${serverNameOrRelativeDllPath}'.`);
        process.exit(1);
    }

    return serverCommand;
}

const argsStart = getArgsStart();
const [serverNameOrRelativeDllPath, ...serverArgs] = process.argv.slice(argsStart);
if (!serverNameOrRelativeDllPath) {
    console.error('Usage: run-prebuilt-mcp.js <server-name|relative-dll-path> [args...]');
    process.exit(1);
}

// Vally starts MCP servers from per-trial temp directories, so resolve staged
// DLL paths from the checked-out repo root instead of from process.cwd().
const [relativeDllPath, ...defaultServerArgs] = resolveServerCommand(serverNameOrRelativeDllPath);
const dllPath = path.resolve(root, relativeDllPath);
const child = spawn('dotnet', [dllPath, ...(serverArgs.length > 0 ? serverArgs : defaultServerArgs)], {
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