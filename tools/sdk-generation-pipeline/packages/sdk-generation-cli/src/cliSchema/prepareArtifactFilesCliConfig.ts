import convict from 'convict';

import { assertNullOrEmpty } from '../utils/validator';

export class PrepareArtifactFilesInput {
    generateAndBuildOutputFile: string;
    artifactDir: string;
    language: string;
}

export const prepareArtifactFilesInput = convict<PrepareArtifactFilesInput>({
    generateAndBuildOutputFile: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'generateAndBuildOutputFile'
    },
    artifactDir: {
        doc: 'The dir to publish artifact',
        default: null,
        format: assertNullOrEmpty,
        arg: 'artifactDir'
    },
    language: {
        default: null,
        format: ['js', 'python', 'go', 'net', 'java'],
        arg: 'language'
    }
});

export class PrepareResultArtifactInput {
    pipelineBuildId: string;
    trigger: string;
    artifactDir: string;
    logPath?: string;
    resultsPath?: string;
}

export const prepareResultArtifactInput = convict<PrepareResultArtifactInput>({
    pipelineBuildId: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'buildId'
    },
    trigger: {
        default: null,
        format: assertNullOrEmpty,
        arg: 'trigger'
    },
    artifactDir: {
        doc: 'The dir to publish artifact',
        default: null,
        format: assertNullOrEmpty,
        arg: 'artifactDir'
    },
    logPath: {
        default: null,
        nullable: true,
        format: String,
        arg: 'logPath'
    },
    resultsPath: {
        doc: 'task result files array',
        default: null,
        nullable: true,
        format: String,
        arg: 'resultsPath'
    }
});
