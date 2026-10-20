// Manual diagnostic: isolate session options using the same scoped installation token.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { buildCases, inspectMcp, redact, sdkImportPath, summarizeTrajectory } from './copilot-app-mcp-probe.mjs';

const TOOL = 'azsdk_get_github_user_details';

export function optionCases() {
    return [
        { name: 'vally-native-fs-control', nativeFs: true },
        { name: 'vally-without-native-fs', nativeFs: false },
        { name: 'vally-explicit-session-start', nativeFs: true, explicitStart: true },
        { name: 'vally-github-token-name', nativeFs: true, tokenName: 'GITHUB_TOKEN' },
        { name: 'direct-sdk-auto-login', direct: true, nativeFs: false, useLoggedInUser: true },
        { name: 'direct-sdk-no-auto-login', direct: true, nativeFs: false, useLoggedInUser: false },
        { name: 'direct-sdk-native-fs', direct: true, nativeFs: true, useLoggedInUser: true },
        { name: 'direct-sdk-native-fs-no-auto-login', direct: true, nativeFs: true, useLoggedInUser: false },
    ];
}

export function selectCases(startupOnly) {
    const cases = optionCases();
    if (!startupOnly) return cases;
    const control = cases.find(({ name }) => name === 'vally-native-fs-control');
    const candidate = cases.find(({ explicitStart }) => explicitStart);
    return [control, ...[1, 2, 3].map((trial) => ({ ...candidate, name: `${candidate.name}-${trial}` }))];
}

export async function startConfiguredMock(session, config, before) {
    const name = 'azure-sdk-mcp';
    assert.equal(before.mcp3pEnabled, true, 'Do not start MCP when third-party servers are not policy-enabled');
    assert.ok(!before.disabledServers?.includes(name), 'Do not start an explicitly disabled server');
    assert.ok(!before.filteredServers?.includes(name), 'Do not start a policy-filtered server');
    assert.equal(config[name].command, 'dotnet');
    assert.deepEqual(config[name].args, ['../artifacts/mcp/mock/azsdk-mock.dll']);
    assert.ok(path.isAbsolute(config[name].cwd));
    await session.rpc.mcp.startServer({ serverName: name, config: config[name] });
}

async function main() {
    const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
    const token = process.env.COPILOT_GITHUB_TOKEN;
    assert.ok(token?.startsWith('ghs_'), 'The unchanged shared login must supply an installation token');
    const alternateNames = ['GH_TOKEN', 'GITHUB_TOKEN', 'COPILOT_GITHUB_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'CAPI_HMAC_KEY', 'COPILOT_HMAC_KEY'];
    const baseEnv = { ...process.env };
    for (const key of alternateNames) delete baseEnv[key];
    const output = path.join(source, 'artifacts/mcp-controls');
    await mkdir(output, { recursive: true });
    const require = createRequire(path.join(source, 'eng/common/scripts/eval/package.json'));
    const sdkDir = path.join(source, 'eng/common/scripts/eval/node_modules/@github/copilot-sdk');
    const sdkPackage = JSON.parse(await readFile(path.join(sdkDir, 'package.json'), 'utf8'));
    const { CopilotClient, approveAll } = await import(pathToFileURL(sdkImportPath(sdkDir, sdkPackage)).href);
    const executorEntry = require.resolve('@microsoft/vally/executor');
    const { CopilotSdkExecutor } = await import(pathToFileURL(executorEntry).href);
    const { LocalSessionFsHandler } = await import(pathToFileURL(path.join(path.dirname(executorEntry), 'local-session-fs-handler.js')).href);
    const { ProjectContext } = await import(pathToFileURL(require.resolve('@microsoft/vally/config')).href);
    const context = await ProjectContext.load(path.join(source, 'evals'));
    const serverConfig = buildCases(context.config.environments['azsdk-mcp-mock'].mcpServers, context.dir)[2].servers;
    const prompt = 'Get details for GitHub user octocat. Use the available Azure SDK MCP tools instead of shell commands or a CLI. Do not inspect or edit workspace files.';
    const records = [];
    const originalEnv = { ...process.env };
    const originalCwd = process.cwd();
    const originalCreate = CopilotClient.prototype.createSession;
    let active;
    let activeCase;
    let activeSession;
    CopilotClient.prototype.createSession = async function (config) {
        const record = active;
        record.client = {
            useLoggedInUser: this.options.useLoggedInUser,
            sessionFs: this.sessionFsConfig != null,
            mode: this.options.mode,
            installationTokenEnvPresent: this.resolvedEnv.COPILOT_GITHUB_TOKEN != null,
            githubTokenEnvPresent: this.resolvedEnv.GITHUB_TOKEN != null,
        };
        record.events = [];
        const onEvent = config.onEvent;
        const session = await originalCreate.call(this, {
            ...config,
            onEvent: (event) => {
                onEvent?.(event);
                if (/mcp|session\.(error|warning|info)/.test(event.type)) {
                    const { name, serverName, status, message, error } = event.data ?? {};
                    record.events.push({ type: event.type, name, serverName, status, message, error });
                }
            },
        });
        activeSession = session;
        record.sessionObserved = true;
        record.before = await inspectMcp(session);
        try {
            const auth = await this.getAuthStatus();
            record.auth = { isAuthenticated: auth.isAuthenticated, authType: auth.authType };
        } catch (error) {
            record.authError = error.message;
        }
        if (activeCase.explicitStart) {
            await startConfiguredMock(session, config.mcpServers, record.before);
            record.afterExplicitStart = await inspectMcp(session);
            assert.equal(record.afterExplicitStart.requiredToolAvailable, true, 'Explicit startup did not expose the required mock tool');
        }
        return session;
    };
    process.chdir(context.dir);
    try {
        for (const testCase of selectCases(process.argv.includes('--startup-only'))) {
            activeCase = testCase;
            active = { name: testCase.name, tokenName: testCase.tokenName ?? 'COPILOT_GITHUB_TOKEN' };
            activeSession = undefined;
            records.push(active);
            for (const key of alternateNames) delete process.env[key];
            process.env[active.tokenName] = token;
            const workspace = await mkdtemp(path.join(tmpdir(), 'mcp-options-'));
            const home = path.join(workspace, 'home');
            const workDir = path.join(workspace, 'work');
            const sessionDir = path.join(workspace, 'session');
            await mkdir(home);
            await mkdir(workDir);
            await mkdir(sessionDir);
            let executor;
            let client;
            try {
                if (testCase.direct) {
                    client = new CopilotClient({
                        env: { ...baseEnv, [active.tokenName]: token, COPILOT_HOME: home },
                        workingDirectory: context.dir,
                        useLoggedInUser: testCase.useLoggedInUser,
                        ...(testCase.nativeFs ? { sessionFs: {
                            initialCwd: context.dir,
                            sessionStatePath: 'session-state',
                            conventions: process.platform === 'win32' ? 'windows' : 'posix',
                        } } : {}),
                    });
                    const mcpServers = structuredClone(serverConfig);
                    mcpServers['azure-sdk-mcp'].tools = ['*'];
                    mcpServers['azure-sdk-mcp'].timeout = 30000;
                    const session = await client.createSession({
                        model: 'gpt-6.1-sol',
                        workingDirectory: workDir,
                        configDirectory: home,
                        skillDirectories: [],
                        streaming: false,
                        mcpServers,
                        onPermissionRequest: approveAll,
                        ...(testCase.nativeFs ? {
                            infiniteSessions: { enabled: false },
                            createSessionFsProvider: () => new LocalSessionFsHandler(sessionDir),
                            largeOutput: { outputDirectory: path.join(sessionDir, 'large-tool-output') },
                        } : {}),
                    });
                    const calls = [];
                    const results = [];
                    session.on((event) => {
                        if (event.type === 'tool.execution_start') calls.push(event.data);
                        if (event.type === 'tool.execution_complete') results.push(event.data);
                    });
                    const response = await session.sendAndWait({ prompt }, 90000);
                    active.result = {
                        output: response?.data.content?.slice(0, 600),
                        toolNames: calls.map(({ toolName }) => toolName),
                        requiredToolSucceeded: calls.some(({ toolName, toolCallId }) => toolName.endsWith(TOOL) &&
                            results.some((result) => result.toolCallId === toolCallId && result.success === true)),
                    };
                } else {
                    executor = new CopilotSdkExecutor({ flushWindowMs: 0 });
                    const trajectory = await executor.execute({ name: testCase.name, prompt }, {
                        model: 'gpt-6.1-sol',
                        timeout: 90000,
                        workDir,
                        mcpServers: serverConfig,
                        ...(testCase.nativeFs ? { sessionLog: { rootDir: sessionDir, sessionID: randomUUID() } } : {}),
                    });
                    active.result = summarizeTrajectory(trajectory);
                }
                assert.ok(active.sessionObserved, 'The observer must attach to the actual session');
            } catch (error) {
                active.error = error.message;
            } finally {
                if (activeSession) {
                    try { active.after = await inspectMcp(activeSession); } catch (error) { active.afterError = error.message; }
                }
                if (executor) await executor.shutdown();
                if (client) await client.stop();
                await rm(workspace, { recursive: true, force: true });
                console.log(`MCP_OPTIONS ${redact(JSON.stringify(active), token)}`);
            }
        }
    } finally {
        CopilotClient.prototype.createSession = originalCreate;
        for (const key of alternateNames) {
            if (originalEnv[key] === undefined) delete process.env[key];
            else process.env[key] = originalEnv[key];
        }
        process.chdir(originalCwd);
        const summary = redact(JSON.stringify({ sdk: sdkPackage.version, originalMockBuild: 6980135, records }, null, 2), token);
        await writeFile(path.join(output, 'options.json'), summary);
        console.log(`MCP_OPTIONS_SUMMARY ${summary}`);
    }
    // This diagnostic is only green if it proves at least one real successful MCP invocation.
    const candidates = records.filter(({ name }) => name.startsWith('vally-explicit-session-start'));
    if (process.argv.includes('--startup-only')) {
        if (candidates.length !== 3 || !candidates.every(({ result }) => result?.requiredToolSucceeded)) process.exitCode = 1;
    } else if (!records.some(({ result }) => result?.requiredToolSucceeded)) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch((error) => {
        console.error(redact(error.message, process.env.COPILOT_GITHUB_TOKEN ?? 'unused-secret-placeholder'));
        process.exitCode = 1;
    });
}