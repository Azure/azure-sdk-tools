import * as convict from 'convict';
import {taskBasicConfig, TaskBasicConfig} from '@azure-tools/sdk-generation-lib';

export class RunGenerateAndBuildTaskCliConfig extends TaskBasicConfig {
    specFolder: string;
    headSha: string;
    headRef: string;
    repoHttpsUrl: string;
    relatedReadmeMdFile: string;
    serviceType: string;
    language: string;
    generateAndBuildInputJson: string;
    generateAndBuildOutputJson: string;
    resourceProvider: string;
}

export const runGenerateAndBuildTaskCliConfig = convict<RunGenerateAndBuildTaskCliConfig>({
    specFolder: {
        default: '',
        env: 'SPEC_FOLDER',
        format: String
    },
    headSha: {
        default: '',
        env: 'HEAD_SHA',
        format: String
    },
    headRef: {
        default: '',
        env: 'HEAD_REF',
        format: String
    },
    repoHttpsUrl: {
        default: 'https://github.com/Azure/azure-rest-api-specs',
        env: 'REPO_HTTP_URL',
        format: String
    },
    relatedReadmeMdFile: {
        default: '',
        env: 'RELATED_README_MD_FILE',
        format: String
    },
    serviceType: {
        default: 'resource-manager',
        env: 'SERVICE_TYPE',
        format: String
    },
    language: {
        default: 'unknown',
        env: 'LANGUAGE',
        format: String
    },
    resourceProvider: {
        default: 'unknown',
        env: "RESOURCE_PROVIDER",
        format: String
    },
    generateAndBuildInputJson: {
        default: '/tmp/generateAndBuildInput.json',
        env: 'GENERATE_AND_BUILD_INPUT_JSON',
        format: String
    },
    generateAndBuildOutputJson: {
        default: '/tmp/generateAndBuildOutput.json',
        env: 'GENERATE_AND_BUILD_OUTPUT_JSON',
        format: String
    },
    ...taskBasicConfig
});
