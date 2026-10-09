import { beforeAll, beforeEach, afterEach, describe, expect, it, vi } from 'vitest';

const { execFileSync, readFile, mkdtemp, writeFile, rm, delay } = vi.hoisted(() => ({
    execFileSync: vi.fn(), readFile: vi.fn(), mkdtemp: vi.fn(), writeFile: vi.fn(), rm: vi.fn(), delay: vi.fn()
}));
vi.mock('node:child_process', () => ({ execFileSync }));
vi.mock('node:fs/promises', () => ({ readFile, mkdtemp, writeFile, rm }));
vi.mock('node:timers/promises', () => ({ setTimeout: delay }));

// The deployment script is native JavaScript, outside the TypeScript application build.
const script = '../scripts/deploy-webjob.mjs';
let deploy: (env: Record<string, string>) => Promise<void>;
beforeAll(async () => { ({ deploy } = await import(script)); });
const settings = { schedule: '0 0 2 * * *', is_singleton: true };
const env = { WEBJOB_ZIP: 'knowledge-sync.zip', APP_NAME: 'test-app', RESOURCE_GROUP: 'test-rg', WEBJOB_NAME: 'knowledge-sync' };
const fetchMock = vi.fn();

describe('WebJob deployment', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        vi.stubGlobal('fetch', fetchMock);
        vi.spyOn(console, 'warn').mockImplementation(() => {});
        vi.spyOn(console, 'log').mockImplementation(() => {});
        readFile.mockImplementation(async (file) => file instanceof URL ? JSON.stringify(settings) : Buffer.from('zip'));
        mkdtemp.mockResolvedValue('webjob-verify-test');
        writeFile.mockResolvedValue(undefined);
        rm.mockResolvedValue(undefined);
        delay.mockResolvedValue(undefined);
        execFileSync.mockImplementation((_command, args: string[]) => {
            if (args[0] === 'account') return JSON.stringify({ accessToken: 'secret-token' });
            if (args[1] === 'show') return JSON.stringify({ reserved: true, kind: 'app,linux,container', enabledHostNames: ['test-app.scm.azurewebsites.net'] });
            if (args.includes('list')) return JSON.stringify([
                { name: 'AZURE_APPCONFIG_ENDPOINT', value: 'https://test.azconfig.io' },
                { name: 'AZURE_CLIENT_ID', value: 'test-client' }
            ]);
            return '{}';
        });
        fetchMock.mockImplementation(async (_url, options) => {
            if (fetchMock.mock.calls.length === 1) return new Response(null, { status: 404 });
            return new Response(options.method === 'PUT' ? null : JSON.stringify({ name: env.WEBJOB_NAME, settings }), { status: 200 });
        });
    });

    afterEach(() => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
    });

    it('uploads with the required ZIP filename and retains Entra authentication', async () => {
        await deploy(env);
        const upload = fetchMock.mock.calls.find(([, options]) => options.headers['Content-Type'] === 'application/zip');
        expect(upload?.[1].headers).toMatchObject({
            'Content-Disposition': 'attachment; filename="knowledge-sync.zip"',
            'Content-Type': 'application/zip',
            Authorization: 'Bearer secret-token'
        });
        expect(fetchMock).toHaveBeenCalledTimes(5);
    });

    it.each([400, 401, 403, 503])('reports upload HTTP %i without exposing response contents', async (status) => {
        fetchMock.mockResolvedValueOnce(new Response(null, { status: 404 }))
            .mockResolvedValueOnce(new Response('secret-token private response', { status }));
        await expect(deploy(env)).rejects.toThrow(`SCM PUT /api/triggeredwebjobs/knowledge-sync rejected: HTTP ${status}`);
        expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('reports a restart/network failure without leaking the underlying error', async () => {
        fetchMock.mockResolvedValueOnce(new Response(null, { status: 404 }))
            .mockRejectedValueOnce(new Error('secret-token private network details'));
        await expect(deploy(env)).rejects.toThrow('SCM PUT /api/triggeredwebjobs/knowledge-sync failed before receiving an HTTP response');
    });

    function timedOutUpload() {
        const cli = execFileSync.getMockImplementation()!;
        execFileSync.mockImplementation((command, args) => command === 'unzip'
            ? '  123 Defl:N 99 20% 2026-10-09 02:00 abcd1234 dist/src/index.js\n'
            : cli(command, args));
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (options.headers['Content-Type'] === 'application/zip') {
                return new Response('One or more errors occurred. (A task was canceled.)', { status: 400 });
            }
            if (url.includes('/api/zip/')) return new Response(Buffer.from('downloaded zip'));
            return normal(url, options);
        });
    }

    it('recovers from the forwarding timeout only after verifying the installed package', async () => {
        timedOutUpload();
        await deploy(env);
        expect(execFileSync.mock.calls.filter(([command, args]) => command === 'unzip' && args.includes('-v'))).toHaveLength(2);
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
        expect(writeFile).toHaveBeenCalledOnce();
        expect(rm).toHaveBeenCalledWith('webjob-verify-test', { recursive: true, force: true });
    });

    it('waits rather than accepting an old or partially installed package', async () => {
        timedOutUpload();
        const cli = execFileSync.getMockImplementation()!;
        let inventories = 0;
        execFileSync.mockImplementation((command, args) => {
            const listing = cli(command, args);
            return command === 'unzip' && args.includes('-v') && ++inventories === 2 ? listing.replace('abcd1234', '11111111') : listing;
        });
        await deploy(env);
        expect(delay).toHaveBeenCalledOnce();
        expect(writeFile).toHaveBeenCalledTimes(2);
    });

    it('verifies completion after a 100-second HTTP 400 even when Kudu hides the error body', async () => {
        timedOutUpload();
        let clock = 0;
        vi.spyOn(Date, 'now').mockImplementation(() => clock);
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (options.headers['Content-Type'] === 'application/zip') {
                clock += 100000;
                return new Response(null, { status: 400 });
            }
            return normal(url, options);
        });
        await deploy(env);
        expect(writeFile).toHaveBeenCalledOnce();
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
    });

    it('fails bounded recovery when the intended package never appears', async () => {
        timedOutUpload();
        let clock = 0;
        vi.spyOn(Date, 'now').mockImplementation(() => clock);
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (fetchMock.mock.calls.length > 2) return new Response(null, { status: 404 });
            return normal(url, options);
        });
        delay.mockImplementation(async () => { clock += 15000; });
        await expect(deploy(env)).rejects.toThrow('within 10 minutes');
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
        expect(rm).toHaveBeenCalledOnce();
    });

    it('does not recover from authorization failures even if the body mentions cancellation', async () => {
        fetchMock.mockResolvedValueOnce(new Response(null, { status: 404 }))
            .mockResolvedValueOnce(new Response('A task was canceled.', { status: 403 }));
        await expect(deploy(env)).rejects.toThrow('HTTP 403');
        expect(mkdtemp).not.toHaveBeenCalled();
    });

    it('identifies a failed CLI configuration operation without leaking subprocess output', async () => {
        execFileSync.mockImplementationOnce(() => JSON.stringify({ reserved: true, kind: 'container', enabledHostNames: ['test-app.scm.azurewebsites.net'] }))
            .mockImplementationOnce(() => JSON.stringify([
                { name: 'AZURE_APPCONFIG_ENDPOINT', value: 'endpoint' },
                { name: 'AZURE_CLIENT_ID', value: 'client' }
            ]))
            .mockImplementationOnce(() => JSON.stringify({ accessToken: 'secret-token' }))
            .mockImplementationOnce(() => { throw Object.assign(new Error('secret-token'), { status: 1, stderr: 'private settings' }); });
        await expect(deploy(env)).rejects.toThrow('Azure CLI webapp config set failed (exit status 1)');
        expect(fetchMock).toHaveBeenCalledTimes(1);
    });
});
