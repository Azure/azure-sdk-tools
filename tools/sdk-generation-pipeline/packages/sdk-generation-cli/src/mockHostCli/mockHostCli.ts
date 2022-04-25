#!/usr/bin/env node

import { mockHostCliSchema } from "./schema/mockHostCliSchema";
import { execSync, spawn } from "child_process";
import { createWriteStream } from "fs";
import * as path from "path";

async function runMockHost() {
    const inputParams = mockHostCliSchema.getProperties();
    const swaggerJsonFilePattern = inputParams.readmeMdPath
        ? inputParams.readmeMdPath.replace(/readme[.a-z-]*.md/gi, '**/*.json')
        : undefined;
    // const logging = createWriteStream(path.join(inputParams.resultOutputFolder, inputParams.mockHostLogger), {flags: 'a'});
    execSync(`node node_modules/@azure-tools/mock-service-host/dist/src/main.js`, {
        cwd: inputParams.mockHostPath,
        env: {
            ...process.env,
            'specRetrievalMethod': 'filesystem',
            'specRetrievalLocalRelativePath': inputParams.specRepo,
            'validationPathsPattern': swaggerJsonFilePattern
        },
        stdio: 'inherit'
    });
    // const mockHostProcess = spawn(`node node_modules/@azure-tools/mock-service-host/dist/src/main.js`, {
    //     cwd: inputParams.mockHostPath,
    //     env: {
    //         specRetrievalMethod: 'filesystem',
    //         specRetrievalLocalRelativePath: inputParams.specRepo,
    //         validationPathsPattern: swaggerJsonFilePattern
    //     }
    // });
    // mockHostProcess.stdout.pipe(logging);
    // mockHostProcess.stderr.pipe(logging);
    // mockHostProcess.on('close', code => {
    //     console.log(`mock host exit with code: ${code}`);
    // });
}

runMockHost().catch(e => {
    console.error("\x1b[31m", e.toString());
    process.exit(1);
})