import { MockChildProcess, mockChildProcessModule } from './mockChildProcess';

export function wait(timeoutOrPromise: number | Promise<unknown> = 10): Promise<void> {
    if (timeoutOrPromise && typeof timeoutOrPromise === 'object' && typeof timeoutOrPromise.then === 'function') {
        return timeoutOrPromise.then(() => wait());
    }

    return new Promise((ok) => setTimeout(ok, typeof timeoutOrPromise === 'number' ? timeoutOrPromise : 10));
}

async function exitChildProcess(proc: MockChildProcess, data: string | null, exitSignal: number) {
    if (proc.$emitted('exit')) {
        throw new Error('exitChildProcess: attempting to exit an already closed process');
    }

    if (typeof data === 'string') {
        proc.stdout.$emit('data', Buffer.from(data));
    }

    proc.$emit('exit', exitSignal);
    proc.$emit('close', exitSignal);
}

export async function closeWithSuccess(message = '') {
    await wait();
    const match = mockChildProcessModule.$matchingChildProcess((p) => !p.$emitted('exit'));
    if (!match) {
        throw new Error(`closeWithSuccess unable to find matching child process`);
    }
    await exitChildProcess(match, message, 0);
    await wait();
}
