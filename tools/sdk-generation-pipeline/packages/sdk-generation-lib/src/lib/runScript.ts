import {RunOptions} from "../types/taskInputAndOuputSchemaTypes/CodegenToSdkConfig";
import * as path from "path";
import {spawn} from "child_process";
import {logger} from "../utils/logger";
import {Readable} from "stream";
import {scriptRunningState} from "../types/scriptRunningState";
import * as fs from "fs";

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
        logger.log(logType, `${prefix} ${line}`, {show: true});
    };

    stream.on('data', (data) => {
        addLine(data.toString());
    });
};

export async function runScript(runOptions: RunOptions, options: {
    cwd: string;
    args?: string[];
}): Promise<string> {
    let executeResult: scriptRunningState;
    const scriptCmd = runOptions.script;
    const scriptPath = runOptions.path.trim();
    const env = {PWD: path.resolve(options.cwd), ...process.env};
    for (const e of runOptions.envs) {
        env[e] = process.env[e];
    }
    let cmdRet: { code: number | null; signal: NodeJS.Signals | null } = {
        code: null,
        signal: null
    };
    logger.log('cmdout', "task script path:" + path.join(options.cwd, scriptPath) );
    if (fs.existsSync(path.join(options.cwd, scriptPath))) {
        logger.log('cmdout', "chmod");
        fs.chmodSync(path.join(options.cwd, scriptPath), '777');
    }

    try {
        let command: string = "";
        let args:string[] = [];
        const scriptPaths: string[] = scriptPath.split(" ");
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
            executeResult = 'succeeded';
        } else {
            executeResult = 'failed';
        }

    } catch (e) {
        cmdRet.code = -1;
        logger.error(`${e.message}\n${e.stack}`);
        executeResult = 'failed';
    }
    let storeLog = false;
    if ((cmdRet.code !== 0 || cmdRet.signal !== null) && runOptions.exitWithNonZeroCode !== undefined) {
        if (runOptions.exitWithNonZeroCode.storeLog) {
            storeLog = true;
        }
        if (runOptions.exitWithNonZeroCode.result === 'error') {
            executeResult = 'failed';
        } else if (runOptions.exitWithNonZeroCode.result === 'warning') {
            executeResult = 'warning';
        }
        const message = `Script return with result [${executeResult}] code [${cmdRet.code}] signal [${cmdRet.signal}] cwd [${options.cwd}]: ${scriptPath}`;
        if (runOptions.exitWithNonZeroCode.result === 'error') {
            logger.error(message, {show: storeLog});
        } else if (runOptions.exitWithNonZeroCode.result === 'warning') {
            logger.warn(message, {show: storeLog});
        }
    }
    return executeResult;
}
