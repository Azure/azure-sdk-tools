import { spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { Readable } from 'stream';
import { Logger } from 'winston';

import { StringMap, TaskResultStatus } from '../types';
import { RunOptions } from '../types/taskInputAndOuputSchemaTypes/CodegenToSdkConfig';
import { logger as globalLogger } from '../utils/logger';

let logger = globalLogger;

export const isLineMatch = (line: string, filter: RegExp | undefined) => {
    if (filter === undefined) {
        return false;
    }
    filter = new RegExp(filter);
    return filter.exec(line) !== null;
};

const listenOnStream = (
    prefix: string,
    stream: Readable,
    logType: 'cmdout' | 'cmderr'
) => {
    const addLine = (line: string) => {
        if (line.length === 0) {
            return;
        }
        logger.log(logType, `${prefix} ${line}`);
    };

    stream.on('data', (data) => {
        addLine(data.toString());
    });
};

export async function runScript(runOptions: RunOptions, options: {
    cwd: string;
    args?: string[];
    envs?: StringMap<string | boolean | number>;
    customizedLogger?: Logger;
}): Promise<TaskResultStatus> {
    if (!!options?.customizedLogger) {
        logger = options.customizedLogger;
    }

    let executeResult: TaskResultStatus;
    const scriptCmd = runOptions.script;
    const scriptPath = runOptions.path.trim();
    const env = { ...process.env, PWD: path.resolve(options.cwd), ...options.envs };

    for (const e of runOptions.envs) {
        env[e] = process.env[e];
    }
    let cmdRet: { code: number | null; signal: NodeJS.Signals | null } = {
        code: null,
        signal: null
    };
    logger.log('cmdout', 'task script path:' + path.join(options.cwd, scriptPath) );
    if (fs.existsSync(path.join(options.cwd, scriptPath))) {
        logger.log('cmdout', 'chmod');
        fs.chmodSync(path.join(options.cwd, scriptPath), '777');
    }

    try {
        let command: string = '';
        let args:string[] = [];
        const scriptPaths: string[] = scriptPath.split(' ');
        if (scriptCmd !== undefined && scriptCmd.length > 0) {
            command = scriptCmd;
            args = args.concat(scriptPaths);
        } else {
            command = scriptPaths[0];
            args = args.concat(scriptPaths.slice(1));
        }
        args = args.concat(options.args);
        const child = spawn(command, args, {
            cwd: options.cwd,
            shell: false,
            stdio: ['ignore', 'pipe', 'pipe'],
            env
        });
        const prefix = `[${runOptions.logPrefix ?? path.basename(scriptPath)}]`;
        listenOnStream(prefix, child.stdout, 'cmdout');
        listenOnStream(prefix, child.stderr, 'cmderr');

        cmdRet = await new Promise((resolve) => {
            child.on('exit', (code, signal) => {
                resolve({ code, signal });
            });
        });
        if (cmdRet.code === 0) {
            executeResult = TaskResultStatus.Success;
        } else {
            executeResult = TaskResultStatus.Failure;
        }
    } catch (e) {
        cmdRet.code = -1;
        logger.error(`${e.message}\n${e.stack}`);
        executeResult = TaskResultStatus.Failure;
    }
    if (cmdRet.code !== 0 || cmdRet.signal !== null) {
        executeResult = TaskResultStatus.Failure;
        const message = `Script return with result [${executeResult}] code [${cmdRet.code}] signal [${cmdRet.signal}] cwd [${options.cwd}]: ${scriptPath}`;
        logger.log('cmderr', message);
    }
    return executeResult;
}
