// Compare runtimes in temporary directories; never update the production dependency lock.
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { buildCases, inspectMcp, redact, sdkImportPath } from './copilot-app-mcp-probe.mjs';

export function versionCases() {
    return [
        { name: 'pinned-sdk-pinned-runtime', sdk: 'pinned', runtime: 'pinned', autoLogin: true },
        { name: 'pinned-sdk-stable-runtime', sdk: 'pinned', runtime: 'stable', autoLogin: true },
        { name: 'stable-sdk-stable-runtime', sdk: 'stable', runtime: 'stable', autoLogin: true },
        { name: 'stable-sdk-stable-runtime-no-auto-login', sdk: 'stable', runtime: 'stable', autoLogin: false },
    ];
}

export function selectDiagnostics(text, token) {
    // Publish only MCP startup/filtering messages. Never persist raw runtime logs or request payloads.
    return text.split(/\r?\n/).filter((line) =>
        /MCP server .*filtered|Failed to .*MCP|MCP server .*failed|Skipping server .*invalid|MCP host .*fail/i.test(line) &&
        !/authorization|bearer\s|password|session\.create request|token\s*[:=]/i.test(line))
        .map((line) => redact(line, token).slice(0, 600)).slice(-20);
}

async function collectDiagnostics(dir, token) {
    const lines = [];
    for (const entry of await readdir(dir, { recursive: true, withFileTypes: true })) {
        if (!entry.isFile() || !/\.log$/.test(entry.name)) continue;
        const parent = entry.parentPath ?? entry.path;
        lines.push(...selectDiagnostics(await readFile(path.join(parent, entry.name), 'utf8'), token));
    }
    return lines.slice(-25);
}

async function loadSdk(dir) {
    const pkg = JSON.parse(await readFile(path.join(dir, 'node_modules/@github/copilot-sdk/package.json'), 'utf8'));
    const sdk = await import(pathToFileURL(sdkImportPath(path.join(dir, 'node_modules/@github/copilot-sdk'), pkg)).href);
    return { ...sdk, packageVersion: pkg.version };
}

async function main() {
    const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
    const token = process.env.COPILOT_GITHUB_TOKEN;
    assert.ok(token?.startsWith('ghs_'), 'The shared App login must supply the installation token');
    const env = { ...process.env };
    for (const key of ['GH_TOKEN', 'GITHUB_TOKEN', 'COPILOT_GITHUB_TOKEN', 'GITHUB_COPILOT_API_TOKEN', 'CAPI_HMAC_KEY', 'COPILOT_HMAC_KEY', 'COPILOT_CLI_PATH']) delete env[key];
    env.COPILOT_GITHUB_TOKEN = token;
    const pinnedDir = path.join(source, 'eng/common/scripts/eval');
    const stableDir = path.join(source, 'artifacts/mcp-upgrade');
    const pinnedRequire = createRequire(path.join(pinnedDir, 'package.json'));
    const stableRequire = createRequire(path.join(stableDir, 'package.json'));
    const sdks = { pinned: await loadSdk(pinnedDir), stable: await loadSdk(stableDir) };
    assert.equal(sdks.pinned.packageVersion, '1.0.11');
    assert.equal(sdks.stable.packageVersion, '1.0.24');
    const platformPackage = `@github/copilot-${process.platform}-${process.arch}`;
    const runtimes = { pinned: pinnedRequire.resolve(platformPackage), stable: stableRequire.resolve(platformPackage) };
    const { ProjectContext } = await import(pathToFileURL(pinnedRequire.resolve('@microsoft/vally/config')).href);
    const context = await ProjectContext.load(path.join(source, 'evals'));
    const servers = buildCases(context.config.environments['azsdk-mcp-mock'].mcpServers, context.dir)[2].servers;
    servers['azure-sdk-mcp'].timeout = 30000;
    servers['azure-sdk-mcp'].tools = ['*'];
    const output = path.join(source, 'artifacts/mcp-controls');
    await mkdir(output, { recursive: true });
    const records = [];
    try {
        for (const testCase of versionCases()) {
            const sdk = sdks[testCase.sdk];
            const record = { name: testCase.name, sdkVersion: sdk.packageVersion, autoLogin: testCase.autoLogin };
            records.push(record);
            const dir = await mkdtemp(path.join(tmpdir(), 'mcp-version-'));
            const home = path.join(dir, 'home');
            const work = path.join(dir, 'work');
            const logs = path.join(dir, 'logs');
            await Promise.all([home, work, logs].map((item) => mkdir(item)));
            const client = new sdk.CopilotClient({
                connection: sdk.RuntimeConnection.forStdio({ path: runtimes[testCase.runtime], args: ['--log-dir', logs] }),
                workingDirectory: context.dir,
                env: { ...env, COPILOT_HOME: home },
                useLoggedInUser: testCase.autoLogin,
                logLevel: 'debug',
            });
            let session;
            try {
                await client.start();
                const status = await client.getStatus();
                record.runtime = { version: status.version, protocolVersion: status.protocolVersion };
                const auth = await client.getAuthStatus();
                record.auth = { isAuthenticated: auth.isAuthenticated, authType: auth.authType };
                session = await client.createSession({
                    model: 'gpt-6.1-sol',
                    workingDirectory: work,
                    configDirectory: home,
                    mcpServers: structuredClone(servers),
                    onPermissionRequest: sdk.approveAll,
                });
                record.before = await inspectMcp(session);
                const calls = [];
                const results = [];
                session.on((event) => {
                    if (event.type === 'tool.execution_start') calls.push(event.data);
                    if (event.type === 'tool.execution_complete') results.push(event.data);
                });
                if (record.before.requiredToolAvailable && record.auth.isAuthenticated) {
                    const response = await session.sendAndWait({ prompt: 'Get details for GitHub user octocat. Use the available Azure SDK MCP tools instead of shell commands or a CLI. Do not inspect or edit workspace files.' }, 90000);
                    record.result = {
                        output: response?.data.content?.slice(0, 600),
                        toolNames: calls.map(({ toolName }) => toolName),
                        requiredToolSucceeded: calls.some(({ toolName, toolCallId }) => toolName.endsWith('azsdk_get_github_user_details') &&
                            results.some((result) => result.toolCallId === toolCallId && result.success === true)),
                    };
                } else {
                    record.skippedInference = 'Required MCP tool or session authentication is unavailable; not a passing tool-use result';
                }
            } catch (error) {
                record.error = error.message;
            } finally {
                if (session) await session.disconnect();
                await client.stop();
                record.diagnostics = await collectDiagnostics(logs, token);
                await rm(dir, { recursive: true, force: true });
                console.log(`MCP_VERSIONS ${redact(JSON.stringify(record), token)}`);
            }
        }
    } finally {
        await writeFile(path.join(output, 'versions.json'), redact(JSON.stringify({ originalMockBuild: 6980135, records }, null, 2), token));
    }
    if (!records.some(({ result }) => result?.requiredToolSucceeded)) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch((error) => {
        console.error(redact(error.message, process.env.COPILOT_GITHUB_TOKEN ?? 'unused-secret-placeholder'));
        process.exitCode = 1;
    });
}