#!/usr/bin/env node
import {
    GenerateAndBuildOutput,
    getGenerateAndBuildOutput,
    logger,
    requireJsonc,
    SDK,
    TaskResultStatus
} from '@azure-tools/sdk-generation-lib';
import * as fs from 'fs';
import * as path from 'path';

import { PrepareArtifactFilesInput, prepareArtifactFilesInput } from '../../cliSchema/prepareArtifactFilesCliConfig';
import { getFileListInPackageFolder } from '../../utils/git';

function copyFile(filePath: string, targetDir: string) {
    const fileDir = path.dirname(filePath);
    fs.mkdirSync(`${targetDir}/${fileDir}`, { recursive: true });
    fs.copyFileSync(`${filePath}`, `${targetDir}/${filePath}`);
}

async function prepareSourceCode(
    generateAndBuildOutput: GenerateAndBuildOutput,
    language: string,
    artifactDir: string
) {
    for (const p of generateAndBuildOutput.packages) {
        const result = p.result;
        if (result === TaskResultStatus.Failure) {
            logger.warn(`Build ${p.packageName} failed, skipped it`);
            continue;
        }
        const packageName = p.packageName;
        const packagePaths = p.path;

        for (const packagePath of packagePaths) {
            if (!fs.existsSync(packagePath)) {
                logger.warn(`${packagePath} isn't exist`);
                continue;
            }

            if (fs.lstatSync(packagePath).isDirectory()) {
                for (const filePath of getFileListInPackageFolder(packagePath)) {
                    copyFile(`${path.join(packagePath, filePath)}`, `${artifactDir}/${language}/${packageName}`);
                }
            } else {
                copyFile(packagePath, `${artifactDir}/${language}/${packageName}`);
            }
        }
    }
}

async function prepareArtifacts(generateAndBuildOutput: GenerateAndBuildOutput, language: string, artifactDir: string) {
    for (const p of generateAndBuildOutput.packages) {
        const result = p.result;
        if (result === TaskResultStatus.Failure) {
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

async function main() {
    prepareArtifactFilesInput.validate();
    const config: PrepareArtifactFilesInput = prepareArtifactFilesInput.getProperties();

    validateInput(config);
    const generateAndBuildOutput: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );

    await prepareSourceCode(generateAndBuildOutput, config.language, config.artifactDir);
    await prepareArtifacts(generateAndBuildOutput, config.language, config.artifactDir);
}

main().catch((e) => {
    logger.error(`${e.message}
     ${e.stack}`);
    process.exit(1);
});
