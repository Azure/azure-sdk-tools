import { beforeAll, beforeEach, afterEach, describe, expect, it, vi } from 'vitest';

const { execFileSync, readFile, delay } = vi.hoisted(() => ({
    execFileSync: vi.fn(), readFile: vi.fn(), delay: vi.fn()
}));
vi.mock('node:child_process', () => ({ execFileSync }));
vi.mock('node:fs/promises', () => ({ readFile }));
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
        delay.mockResolvedValue(undefined);
        execFileSync.mockImplementation((_command, args: string[]) => {
            if (args[0] === 'account') return JSON.stringify({ accessToken: 'secret-token' });
            if (args[1] === 'show') return JSON.stringify({ reserved: true, kind: 'app,linux,container', enabledHostNames: ['test-app.scm.azurewebsites.net'] });
            if (args.includes('list')) return JSON.stringify([
                { name: 'AZURE_APPCONFIG_ENDPOINT', value: 'https://test.azconfig.io' },
                { name: 'AZURE_CLIENT_ID', value: 'test-client' },
                { name: 'BOT_CLIENT_ID', value: 'bot-client' }
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

    it('rejects a missing signing identity before changing the job', async () => {
        const normal = execFileSync.getMockImplementation()!;
        execFileSync.mockImplementation((command, args: string[], options) => {
            const result = normal(command, args, options);
            return args.includes('list')
                ? JSON.stringify(JSON.parse(result).filter((setting: any) => setting.name !== 'BOT_CLIENT_ID'))
                : result;
        });
        await expect(deploy(env)).rejects.toThrow('Required existing app setting missing: BOT_CLIENT_ID');
        expect(fetchMock).not.toHaveBeenCalled();
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

    it.each([400, 401, 403, 500, 503])('reports upload HTTP %i without exposing response contents', async (status) => {
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

    it.each([500, 502, 503, 504])('retries read-only HTTP %i after configuration changes and still checks job history', async (status) => {
        const paused = { name: env.WEBJOB_NAME, settings: { ...settings, schedule: null } };
        fetchMock.mockResolvedValueOnce(new Response(JSON.stringify(paused)))
            .mockResolvedValueOnce(new Response(null))
            .mockResolvedValueOnce(new Response(JSON.stringify(paused)))
            .mockResolvedValueOnce(new Response(JSON.stringify({ runs: [] })))
            .mockResolvedValueOnce(new Response('private details', { status }))
            .mockResolvedValueOnce(new Response(JSON.stringify(paused)))
            .mockResolvedValueOnce(new Response(JSON.stringify({ runs: [] })));
        await deploy(env);
        expect(delay).toHaveBeenCalledWith(15000);
        expect(fetchMock.mock.calls.filter(([url]) => url.endsWith('/history'))).toHaveLength(2);
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
    });

    it('retries read-only network failures without logging private details', async () => {
        fetchMock.mockRejectedValueOnce(new Error('secret-token private network details'))
            .mockResolvedValueOnce(new Response(null, { status: 404 }));
        await deploy(env);
        expect(delay).toHaveBeenCalledWith(15000);
        expect(console.warn).not.toHaveBeenCalledWith(expect.stringContaining('secret-token'));
    });

    it.each([401, 403])('does not retry read-only authorization HTTP %i', async (status) => {
        fetchMock.mockResolvedValueOnce(new Response('private details', { status }));
        await expect(deploy(env)).rejects.toThrow(`SCM GET /api/triggeredwebjobs/knowledge-sync rejected: HTTP ${status}`);
        expect(fetchMock).toHaveBeenCalledOnce();
        expect(delay).not.toHaveBeenCalled();
    });

    it('bounds read-only retries when SCM remains unavailable', async () => {
        let clock = 0;
        vi.spyOn(Date, 'now').mockImplementation(() => clock);
        delay.mockImplementation(async () => { clock += 15000; });
        fetchMock.mockImplementation(async () => new Response('private details', { status: 500 }));
        await expect(deploy(env)).rejects.toThrow('SCM GET /api/triggeredwebjobs/knowledge-sync rejected: HTTP 500');
        expect(clock).toBeLessThanOrEqual(5 * 60 * 1000);
        expect(fetchMock.mock.calls.every(([, options]) => options.method === 'GET')).toBe(true);
    });

    function timedOutUpload() {
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (options.headers['Content-Type'] === 'application/zip') {
                return new Response('One or more errors occurred. (A task was canceled.)', { status: 400 });
            }
            return normal(url, options);
        });
    }

    it('recovers from the forwarding timeout when the job is discoverable', async () => {
        timedOutUpload();
        await deploy(env);
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
        expect(fetchMock.mock.calls.every(([url]) => !url.includes('/api/zip/'))).toBe(true);
    });

    it('polls job metadata until discovery', async () => {
        timedOutUpload();
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (fetchMock.mock.calls.length === 3) return new Response(null, { status: 404 });
            return normal(url, options);
        });
        await deploy(env);
        expect(delay).toHaveBeenCalledOnce();
        expect(fetchMock.mock.calls.every(([url]) => !url.includes('/api/zip/'))).toBe(true);
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
        expect(fetchMock.mock.calls.every(([url]) => !url.includes('/api/zip/'))).toBe(true);
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
    });

    it('fails bounded recovery when the job is not discoverable', async () => {
        timedOutUpload();
        let clock = 0;
        vi.spyOn(Date, 'now').mockImplementation(() => clock);
        const normal = fetchMock.getMockImplementation()!;
        fetchMock.mockImplementation(async (url, options) => {
            if (fetchMock.mock.calls.length > 2) return new Response(null, { status: 404 });
            return normal(url, options);
        });
        delay.mockImplementation(async () => { clock += 15000; });
        await expect(deploy(env)).rejects.toThrow('within 5 minutes');
        expect(fetchMock.mock.calls.filter(([, options]) => options.headers['Content-Type'] === 'application/zip')).toHaveLength(1);
    });

    it('does not recover from authorization failures even if the body mentions cancellation', async () => {
        fetchMock.mockResolvedValueOnce(new Response(null, { status: 404 }))
            .mockResolvedValueOnce(new Response('A task was canceled.', { status: 403 }));
        await expect(deploy(env)).rejects.toThrow('HTTP 403');
        expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('identifies a failed CLI configuration operation without leaking subprocess output', async () => {
        execFileSync.mockImplementationOnce(() => JSON.stringify({ reserved: true, kind: 'container', enabledHostNames: ['test-app.scm.azurewebsites.net'] }))
            .mockImplementationOnce(() => JSON.stringify([
                { name: 'AZURE_APPCONFIG_ENDPOINT', value: 'endpoint' },
                { name: 'AZURE_CLIENT_ID', value: 'client' },
                { name: 'BOT_CLIENT_ID', value: 'bot-client' }
            ]))
            .mockImplementationOnce(() => JSON.stringify({ accessToken: 'secret-token' }))
            .mockImplementationOnce(() => { throw Object.assign(new Error('secret-token'), { status: 1, stderr: 'private settings' }); });
        await expect(deploy(env)).rejects.toThrow('Azure CLI webapp config set failed (exit status 1)');
        expect(fetchMock).toHaveBeenCalledTimes(1);
    });
});
