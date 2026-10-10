import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { load } from 'js-yaml';
import { describe, expect, it, vi } from 'vitest';

const pipeline = load(readFileSync(resolve(__dirname, '../../azure-sdk-qa-bot-agent/pipelines/server-cd.yml'), 'utf8')) as any;
const jobs = pipeline.extends.parameters.stages[0].jobs.flatMap((entry: any) => entry.job ? [entry] : Object.values(entry).flat());
const steps = jobs.find((job: any) => job.job === 'DeployKnowledgeSync').steps;
const tasks = steps.flatMap((step: any) => step.task ? [step] : Object.values(step).filter(Array.isArray).flat());
const task = tasks.find((step: any) => step.displayName === 'Configure Knowledge Sync SSH Key');
const inline = task.inputs.inlineScript as string;
const code = inline.slice(inline.indexOf('\n') + 1, inline.lastIndexOf('NODE')).replace(/^import .*;$/gm, '');
const key = '-----BEGIN OPENSSH PRIVATE KEY-----\nfixture\n-----END OPENSSH PRIVATE KEY-----\n';

function provision(value: string, fail = false) {
    const context = {
        Buffer, readFileSync: vi.fn(() => value), mkdtempSync: vi.fn(() => '/temporary'),
        writeFileSync: vi.fn((_path: string, _data: string, _options: unknown) => {}),
        rmSync: vi.fn(), tmpdir: () => '/tmp', join: (...parts: string[]) => parts.join('/'),
        execFileSync: vi.fn((_command: string, _args: string[], _options: unknown) => { if (fail) throw new Error('private credential details'); }),
        process: { env: { SSH_KEY_FILE: '/secure-file', APP_NAME: 'app', RESOURCE_GROUP: 'rg' }, exitCode: 0 },
        console: { log: vi.fn(), error: vi.fn() }
    };
    runInNewContext(code, context);
    return context;
}

describe('CD SSH secure file provisioning', () => {
    it.each([key, Buffer.from(key).toString('base64')])('provisions raw or encoded key through a private temporary settings file', (value) => {
        const result = provision(value);
        expect(JSON.parse(result.writeFileSync.mock.calls[0][1]).SSH_PRIVATE_KEY).toBe(Buffer.from(key).toString('base64'));
        expect(result.writeFileSync.mock.calls[0][2]).toEqual({ mode: 0o600 });
        expect(result.execFileSync.mock.calls[0][1]).toContain('@/temporary/settings.json');
        expect(JSON.stringify(result.execFileSync.mock.calls)).not.toContain('fixture');
        expect(result.rmSync).toHaveBeenCalledWith('/temporary', { recursive: true, force: true });
        expect(result.process.exitCode).toBe(0);
    });

    it('rejects invalid input before updating App Service', () => {
        const result = provision('invalid');
        expect(result.process.exitCode).toBe(1);
        expect(result.execFileSync).not.toHaveBeenCalled();
    });

    it('cleans temporary credentials and suppresses CLI error contents on failure', () => {
        const result = provision(key, true);
        expect(result.process.exitCode).toBe(1);
        expect(result.rmSync).toHaveBeenCalled();
        expect(JSON.stringify(result.console.error.mock.calls)).not.toContain('private credential details');
    });
});
