import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { runInNewContext } from 'node:vm';
import { transpile } from 'typescript';
import { expect, it, vi } from 'vitest';

it('writes SSH material only to the supplied run directory', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'sync-ssh-test-'));
    try {
        const source = fs.readFileSync(path.resolve(__dirname, '../src/DailySyncKnowledge.ts'), 'utf8');
        const setup = source.slice(source.indexOf('async function setupSSHConfig('), source.indexOf('/**\n * Setup documentation repositories'));
        const env = { HOME: path.join(root, 'home'), SSH_PRIVATE_KEY: Buffer.from('test-key').toString('base64'), GIT_SSH_COMMAND: '' };
        const sshDir = path.join(root, 'run', '.ssh');
        const result = runInNewContext(transpile(setup) + '\nsetupSSHConfig(sshDir);', {
            fs, path, Buffer, process: { env }, sshDir,
            console: { log: vi.fn(), warn: vi.fn(), error: vi.fn() },
            require: () => ({ execSync: () => Buffer.from('') })
        });
        await result;
        expect(fs.readFileSync(path.join(sshDir, 'id_ed25519'), 'utf8')).toBe('test-key');
        expect(env.GIT_SSH_COMMAND).toContain(path.join(sshDir, 'config'));
        expect(fs.existsSync(env.HOME)).toBe(false);
        fs.rmSync(path.join(root, 'run'), { recursive: true, force: true });
        expect(fs.existsSync(sshDir)).toBe(false);
    } finally {
        fs.rmSync(root, { recursive: true, force: true });
    }
});
