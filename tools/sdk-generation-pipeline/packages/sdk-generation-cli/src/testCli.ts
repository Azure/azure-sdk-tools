#!/usr/bin/env node

import * as fs from 'fs';
// import { resultPublisherBlobConfig, ResultPublisherBlobConfig } from './cliSchema/testConfig';
import { prepareArtifactFilesInput, PrepareArtifactFilesInput } from './cliSchema/prepareArtifactFilesCliConfig';
import { GenerateAndBuildOutput, getGenerateAndBuildOutput, requireJsonc, SDK } from '@azure-tools/sdk-generation-lib';

function validateInput(config: PrepareArtifactFilesInput) {
    if (!fs.existsSync(config.generateAndBuildOutputFile)) {
        throw new Error(`generateAndBuildOutputFile:${config.generateAndBuildOutputFile} isn's exist!`);
    }
    if (!fs.existsSync(config.artifactDir)) {
        throw new Error(`Invalid artifactDir:${config.artifactDir}!`);
    }
    if (!(<any>Object).values(SDK).includes(config.language)) {
        throw new Error(config.language + ` is not supported.`);
    }
}

// type AA = {
//     name: string;
// };

// type BB = AA & {
//     job: string;
// };

// type CC = {
//     school: string;
// };

// enum etype {
//     aa = 'aa',
//     bb = 'bb',
// }

async function main() {
    console.log(`main`);
    // resultPublisherBlobConfig.validate();
    // const jsonStr = '{"nam":"release-workflow"}';
    // // const jsonStr = '{"name":"zhou"}';
    // let jsonObj = JSON.parse(jsonStr);
    // console.log(jsonStr);
    // console.log(jsonObj);
    // console.log(jsonObj as BB);
    // console.log(jsonObj as AA);
    // console.log(jsonObj as CC);
    // type TYPEA = 'aa' | 'bb';
    // const t: TYPEA = 'bb';
    // throw new Error(`unsupported TYPEA` + (t as string));
    // const straa = 'aa';
    // if ((straa as etype) === etype.aa) {
    //     console.log('equal');
    // }
    // resultPublisherBlobConfig.validate();
    // const config: ResultPublisherBlobConfig = resultPublisherBlobConfig.getProperties();
    // console.log(`${config.logsAndResultPath}`);
    prepareArtifactFilesInput.validate();
    console.log('00');
    const config: PrepareArtifactFilesInput = prepareArtifactFilesInput.getProperties();
    console.log('11');
    validateInput(config);
    console.log('22');
    const generateAndBuildOutput: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );

    console.log(`main end`);
}

main().catch((e) => {
    console.error(`${e.message}
    ${e.stack}`);
    process.exit(1);
});
