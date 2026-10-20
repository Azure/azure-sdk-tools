// Test-only instrumentation. Production sources and authentication are not modified.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const REQUIRED_TOOL = 'azsdk_get_github_user_details';

export function buildCases(servers, configDir) {
    assert.equal(servers['azure-sdk-mcp'].command, 'dotnet');
    assert.deepEqual(servers['azure-sdk-mcp'].args, ['../artifacts/mcp/mock/azsdk-mock.dll']);
    const configured = structuredClone(servers);
    const absolute = structuredClone(servers);
    absolute['azure-sdk-mcp'].cwd = path.resolve(configDir, configured['azure-sdk-mcp'].cwd ?? '.');
    return [
        { name: 'no-mcp-inference-control', servers: undefined },
        { name: 'configured-relative-cwd', servers: configured },
        { name: 'absolute-cwd-only', servers: absolute },
    ];
}

export function redact(text, secret) {
    return String(text ?? '')
        .replaceAll(secret, '[REDACTED]')
        .replace(/\b(?:ghs_|ghp_|ghu_|github_pat_)[A-Za-z0-9_]+/g, '[REDACTED]')
        .replaceAll('##vso[', '[diagnostic-vso ');
}

export async function inspectMcp(session) {
    const listed = await session.rpc.mcp.list();
    const state = {
        servers: (listed.servers ?? []).map(({ name, status, error, transport }) => ({ name, status, error, transport })),
        mcp3pEnabled: listed.host?.mcp3pEnabled,
        disabledServers: listed.host?.disabledServers,
        filteredServers: listed.host?.filteredServers,
        connectedServers: listed.host?.clients,
    };
    try {
        const { tools } = await session.rpc.mcp.listTools({ serverName: 'azure-sdk-mcp' });
        state.toolCount = tools.length;
        state.requiredToolAvailable = tools.some(({ name }) => name === REQUIRED_TOOL);
    } catch (error) {
        state.listToolsError = error.message;
        state.requiredToolAvailable = false;
    }
    return state;
}

export function summarizeTrajectory(trajectory) {
    const calls = trajectory.events.filter(({ type }) => type === 'tool_call');
    const results = trajectory.events.filter(({ type }) => type === 'tool_result');
    const required = calls.filter(({ data }) => data.toolName.endsWith(REQUIRED_TOOL));
    return {
        model: trajectory.metadata.model,
        output: trajectory.output.slice(0, 600),
        totalTokens: trajectory.metrics.tokenUsage?.totalTokens,
        toolNames: calls.map(({ data }) => data.toolName),
        requiredToolCalled: required.length > 0,
        requiredToolSucceeded: required.some(({ data }) => results.some(({ data: result }) =>
            result.toolCallId === data.toolCallId && result.success === true)),
        errors: trajectory.events.filter(({ type }) => type === 'error').map(({ data }) => data.message),
    };
}

async function main() {
    const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
    const token = process.env.COPILOT_GITHUB_TOKEN;
    assert.ok(token?.startsWith('ghs_'), 'The shared login must supply an installation token');
    // Do not introduce a second auth identity or change the documented production token name.
    for (const name of ['GH_TOKEN', 'GITHUB_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'CAPI_HMAC_KEY', 'COPILOT_HMAC_KEY']) {
        delete process.env[name];
    }
    const output = path.join(source, 'artifacts/mcp-controls');
    await mkdir(output, { recursive: true });
    const require = createRequire(path.join(source, 'eng/common/scripts/eval/package.json'));
    const { CopilotClient } = await import(pathToFileURL(require.resolve('@github/copilot-sdk')).href);
    const { CopilotSdkExecutor } = await import(pathToFileURL(require.resolve('@microsoft/vally/executor')).href);
    const { ProjectContext } = await import(pathToFileURL(require.resolve('@microsoft/vally/config')).href);
    const context = await ProjectContext.load(path.join(source, 'evals'));
    const versions = {};
    for (const name of ['@microsoft/vally', '@github/copilot-sdk', '@github/copilot']) {
        const pkg = JSON.parse(await readFile(path.join(source, 'eng/common/scripts/eval/node_modules', name, 'package.json'), 'utf8'));
        versions[name] = pkg.version;
    }
    const records = [];
    let active;
    const createSession = CopilotClient.prototype.createSession;
    // Observe the session that Vally actually creates; preserve every session option and auth path.
    CopilotClient.prototype.createSession = async function (config) {
        const session = await createSession.call(this, config);
        active.sessionCreated = true;
        active.launch = config.mcpServers;
        try {
            active.mcp = await inspectMcp(session);
        } catch (error) {
            active.inspectionError = error.message;
        }
        return session;
    };
    const originalCwd = process.cwd();
    process.chdir(context.dir);
    try {
        for (const testCase of buildCases(context.config.environments['azsdk-mcp-mock'].mcpServers, context.dir)) {
            active = { name: testCase.name };
            records.push(active);
            const workspace = await mkdtemp(path.join(tmpdir(), 'app-mcp-control-'));
            const executor = new CopilotSdkExecutor({ flushWindowMs: 0 });
            const prompt = testCase.servers
                ? 'Get details for GitHub user octocat. Use the available Azure SDK MCP tools instead of shell commands or a CLI. Do not inspect or edit workspace files.'
                : 'Reply with exactly AUTH_OK and nothing else. Do not use tools.';
            try {
                const trajectory = await executor.execute({ name: testCase.name, prompt }, {
                    model: 'gpt-6.1-sol',
                    timeout: 90000,
                    workDir: workspace,
                    mcpServers: testCase.servers,
                    sessionLog: { rootDir: path.join(workspace, 'session'), sessionID: randomUUID() },
                });
                active.trajectory = summarizeTrajectory(trajectory);
            } catch (error) {
                active.error = error.message;
            } finally {
                await executor.shutdown();
                await rm(workspace, { recursive: true, force: true });
            }
            console.log(`MCP_CONTROL ${redact(JSON.stringify(active), token)}`);
        }
    } finally {
        process.chdir(originalCwd);
        CopilotClient.prototype.createSession = createSession;
        const summary = redact(JSON.stringify({ versions, tokenType: 'installation', tokenVariable: 'COPILOT_GITHUB_TOKEN', records }, null, 2), token);
        await writeFile(path.join(output, 'summary.json'), summary);
        console.log(`MCP_CONTROL_SUMMARY ${summary}`);
    }
    const control = records.find(({ name }) => name === 'no-mcp-inference-control');
    const absolute = records.find(({ name }) => name === 'absolute-cwd-only');
    if (control.trajectory?.output.trim() !== 'AUTH_OK' || !absolute.mcp?.requiredToolAvailable || !absolute.trajectory?.requiredToolSucceeded) {
        process.exitCode = 1;
    }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch((error) => {
        console.error(redact(error.message, process.env.COPILOT_GITHUB_TOKEN ?? 'unused-secret-placeholder'));
        process.exitCode = 1;
    });
}