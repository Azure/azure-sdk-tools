#!/usr/bin/env node
import {
    CompletedEvent,
    GenerateAndBuildOutput,
    generateTotalResult,
    getGenerateAndBuildOutput,
    getTaskResults,
    InProgressEvent,
    logger,
    PipelineRunEvent,
    requireJsonc,
    SDK,
    SDKPipelineStatus,
    TaskResult,
    Trigger
} from '@azure-tools/sdk-generation-lib';
import * as fs from 'fs';
import * as path from 'path';

import {
    PrepareArtifactFilesInput,
    prepareArtifactFilesInput,
    PrepareResultArtifactInput,
    prepareResultArtifactInput
} from '../../cliSchema/prepareArtifactFilesCliConfig';
import { GitOperationWrapper } from '../../utils/GitOperationWrapper';

function copyFile(filePath: string, targetDir: string) {
    if (!fs.existsSync(filePath)) {
        logger.info(`${filePath} isn't exist, skipped it.`);
        return;
    }
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
        const packageName = p.packageName;
        const packagePaths = p.path;

        for (const packagePath of packagePaths) {
            if (!fs.existsSync(packagePath)) {
                logger.warn(`${packagePath} isn't exist`);
                continue;
            }

            if (fs.lstatSync(packagePath).isDirectory()) {
                const gitOperationWrapper: GitOperationWrapper = new GitOperationWrapper(packagePath);

                for (const filePath of await gitOperationWrapper.getFileListInPackageFolder(packagePath)) {
                    copyFile(
                        `${path.join(packagePath, filePath)}`,
                        `${artifactDir}/${language}/sourceCode/${packageName}`
                    );
                }
            } else {
                copyFile(packagePath, `${artifactDir}/${language}/sourceCode/${packageName}`);
            }
        }
    }
}

async function prepareArtifacts(generateAndBuildOutput: GenerateAndBuildOutput, language: string, artifactDir: string) {
    for (const p of generateAndBuildOutput.packages) {
        const artifacts = p.artifacts;
        if (!artifacts) {
            // artifacts is optional
            continue;
        }

        fs.mkdirSync(`${artifactDir}/${language}/artifact`, { recursive: true });
        for (const artifact of artifacts) {
            const artifactName = path.basename(artifact);
            fs.copyFileSync(`${artifact}`, `${artifactDir}/${language}/artifact/${artifactName}`);
        }
    }
}

function validateInput(config: PrepareArtifactFilesInput) {
    if (!fs.existsSync(config.generateAndBuildOutputFile)) {
        logger.error(`generateAndBuildOutputFile:${config.generateAndBuildOutputFile} isn's exist!`);
        process.exit(0);
    }
    if (!fs.existsSync(config.artifactDir)) {
        throw new Error(`Invalid artifactDir:${config.artifactDir}!`);
    }
    if (!(<any>Object).values(SDK).includes(config.language)) {
        throw new Error(config.language + ` is not supported.`);
    }
}

async function prepareSourceCodeAndArtifacts() {
    prepareArtifactFilesInput.validate();
    const config: PrepareArtifactFilesInput = prepareArtifactFilesInput.getProperties();

    validateInput(config);
    const generateAndBuildOutput: GenerateAndBuildOutput = getGenerateAndBuildOutput(
        requireJsonc(config.generateAndBuildOutputFile)
    );

    await prepareSourceCode(generateAndBuildOutput, config.language, config.artifactDir);
    await prepareArtifacts(generateAndBuildOutput, config.language, config.artifactDir);
}

function validateResultInput(config: PrepareResultArtifactInput) {
    if (!fs.existsSync(config.artifactDir)) {
        throw new Error(`Invalid artifactDir:${config.artifactDir}!`);
    }
}

function getTrigger(config: PrepareResultArtifactInput): Trigger {
    let trigger: Trigger;
    try {
        trigger = JSON.parse(config.trigger);
    } catch (error) {
        logger.error(`Wrong json format:` + config.trigger);
        throw new Error(error);
    }

    return trigger;
}

function prepareResult(pipelineStatus: SDKPipelineStatus) {
    prepareResultArtifactInput.validate();
    const config: PrepareResultArtifactInput = prepareResultArtifactInput.getProperties();

    validateResultInput(config);
    const trigger: Trigger = getTrigger(config);
    let event: PipelineRunEvent = undefined;

    switch (pipelineStatus) {
    case 'in_progress':
        event = {
            status: 'in_progress',
            trigger: trigger,
            pipelineBuildId: config.pipelineBuildId
        } as InProgressEvent;
        break;
    case 'completed':
        if (!config.resultsPath || !config.logPath) {
            throw new Error(`Invalid completed event parameter!`);
        }

        const taskResults: TaskResult[] = getTaskResults(config.resultsPath);
        const taskTotalResult: TaskResult = generateTotalResult(taskResults, config.pipelineBuildId);
        event = {
            status: 'completed',
            trigger: trigger,
            pipelineBuildId: config.pipelineBuildId,
            logPath: config.logPath,
            result: taskTotalResult
        } as CompletedEvent;
        break;
    default:
        throw new Error(`Unsupported status: ` + (pipelineStatus as string));
    }

    fs.writeFileSync(config.artifactDir + `/` + pipelineStatus + `/result.json`, JSON.stringify(event, null, 2), {
        encoding: 'utf-8'
    });
}

async function main() {
    const args = parseArgs(process.argv);
    const pipelineStatus = args['pipelineStatus'];

    if (pipelineStatus === 'completed') {
        prepareResult(pipelineStatus);
        await prepareSourceCodeAndArtifacts();
    } else if (pipelineStatus === 'in_progress') {
        prepareResult(pipelineStatus);
    } else {
        throw new Error(`Unknown pipelineStatus:${pipelineStatus}!`);
    }
}

/**
 * Parse a list of command line arguments.
 * @param argv List of cli args(process.argv)
 */
const flagRegex = /^--([^=:]+)([=:](.+))?$/;
export function parseArgs(argv: string[]) {
    const result: any = {};
    for (const arg of argv) {
        const match = flagRegex.exec(arg);
        if (match) {
            const key = match[1];
            const rawValue = match[3];
            result[key] = rawValue;
        }
    }
    return result;
}

main().catch((e) => {
    logger.error(`${e.message}
     ${e.stack}`);
    process.exit(1);
});
