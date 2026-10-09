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
                { name: 'AZURE_CLIENT_ID', value: 'client' }
            ]))
            .mockImplementationOnce(() => JSON.stringify({ accessToken: 'secret-token' }))
            .mockImplementationOnce(() => { throw Object.assign(new Error('secret-token'), { status: 1, stderr: 'private settings' }); });
        await expect(deploy(env)).rejects.toThrow('Azure CLI webapp config set failed (exit status 1)');
        expect(fetchMock).toHaveBeenCalledTimes(1);
    });
});
