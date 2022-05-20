#!/usr/bin/env node
import * as fs from 'fs';
import * as path from 'path';

import { getArtifactFilesInput, GetArtifactFilesInput } from './cliSchema/getArtifactFilesCliConfig';
import {
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    requireJsonc,
    SDK,
} from '@azure-tools/sdk-generation-lib';
import { getFileListInPackageFolder } from './utils/git';

async function getSourceCode(generateAndBuildOutput: GenerateAndBuildOutput, language: string, artifactDir: string) {
    for (const p of generateAndBuildOutput.packages) {
        const result = p.result;
        if (result === 'failed') {
            logger.warn(`Build ${p.packageName} failed, skipped it`);
            continue;
        }
        const packageName = p.packageName;
        const packageFolder = p.packageFolder;

        if (packageFolder && fs.existsSync(packageFolder)) {
            for (const filePath of getFileListInPackageFolder(packageFolder)) {
                if (fs.existsSync(path.join(packageFolder, filePath))) {
                    const fileDir = path.dirname(filePath);
                    fs.mkdirSync(`${artifactDir}/${language}/${packageName}/${fileDir}`, { recursive: true });
                    fs.copyFileSync(
                        `${path.join(packageFolder, filePath)}`,
                        `${artifactDir}/${language}/${packageName}/${filePath}`
                    );
                }
            }
        }
    }
}

async function getArtifacts(generateAndBuildOutput: GenerateAndBuildOutput, language: string, artifactDir: string) {
    for (const p of generateAndBuildOutput.packages) {
        const result = p.result;
        if (result === 'failed') {
            logger.warn(`Build ${p.packageName} failed, skipped it`);
            continue;
        }
        const artifacts = p.artifacts;
        if (!artifacts) {
            // artifacts is optional
            continue;
        }

        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            fs.mkdirSync(`${artifactDir}/${language}`, { recursive: true });
            fs.copyFileSync(`${artifact}`, `${artifactDir}/${language}/${artifactName}`);
        }
    }
}

function validateInput(config: GetArtifactFilesInput) {
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

async function main() {
    getArtifactFilesInput.validate();
    const config: GetArtifactFilesInput = getArtifactFilesInput.getProperties();

    validateInput(config);
    const generateAndBuildOutput: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );

    await getSourceCode(generateAndBuildOutput, config.language, config.artifactDir);
    await getArtifacts(generateAndBuildOutput, config.language, config.artifactDir);
}

main().catch((e) => {
    logger.error(`${e.message}
     ${e.stack}`);
    process.exit(1);
});
