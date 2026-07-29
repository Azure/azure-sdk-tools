const { spawn } = require('child_process');

const separatorIndex = process.argv.indexOf('--');
const commandAndArgs = separatorIndex >= 0
  ? process.argv.slice(separatorIndex + 1)
  : process.argv.slice(2);

if (commandAndArgs.length === 0) {
  throw new Error('Usage: node smoke-test-mcp.js -- <command> [args...]');
}

const [command, ...args] = commandAndArgs;
const timeoutMs = Number.parseInt(process.env.MCP_SMOKE_TEST_TIMEOUT_MS || '30000', 10);
const child = spawn(command, args, {
  stdio: ['pipe', 'pipe', 'pipe'],
  env: process.env,
});

let stdout = '';
let stderr = '';
let completed = false;

const finish = (exitCode, message) => {
  if (completed) {
    return;
  }

  completed = true;
  clearTimeout(timer);
  if (!child.killed) {
    child.kill();
  }

  if (message) {
    console.log(message);
  }

  process.exit(exitCode);
};

const timer = setTimeout(() => {
  finish(1, [
    `Timed out waiting for MCP initialize response from: ${command} ${args.join(' ')}`,
    stderr ? `stderr:\n${stderr}` : '',
    stdout ? `stdout:\n${stdout}` : '',
  ].filter(Boolean).join('\n'));
}, timeoutMs);

child.stdout.on('data', (data) => {
  stdout += data.toString();
  for (const line of stdout.split(/\r?\n/)) {
    if (!line.trim()) {
      continue;
    }

    try {
      const message = JSON.parse(line);
      if (message.id === 1 && message.result) {
        const serverName = message.result.serverInfo?.name || 'unknown';
        finish(0, `MCP initialize succeeded for ${serverName}.`);
      }
    } catch {
      // Keep buffering; startup logs or partial lines are reported on timeout.
    }
  }
});

child.stderr.on('data', (data) => {
  stderr += data.toString();
});

child.on('exit', (code, signal) => {
  if (completed) {
    return;
  }

  finish(1, [
    `MCP process exited before initialize completed. code=${code ?? 'null'} signal=${signal ?? 'null'}`,
    stderr ? `stderr:\n${stderr}` : '',
    stdout ? `stdout:\n${stdout}` : '',
  ].filter(Boolean).join('\n'));
});

const initialize = JSON.stringify({
  jsonrpc: '2.0',
  id: 1,
  method: 'initialize',
  params: {
    protocolVersion: '2024-11-05',
    capabilities: {},
    clientInfo: {
      name: 'pipeline-mcp-smoke-test',
      version: '1.0.0',
    },
  },
});

child.stdin.write(`${initialize}\n`);