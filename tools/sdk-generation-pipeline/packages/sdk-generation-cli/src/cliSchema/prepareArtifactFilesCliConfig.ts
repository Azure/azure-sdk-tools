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
        env: 'GENERATE_AND_BUILD_OUTPUTFILE',
    },
    artifactDir: {
        doc: 'The dir to publish artifact',
        default: null,
        env: 'ARTIFACT_DIR',
        format: assertNullOrEmpty,
    },
    language: {
        default: null,
        env: 'LANGUAGE',
        format: ['js', 'python', 'go', 'net', 'java'],
    },
});
