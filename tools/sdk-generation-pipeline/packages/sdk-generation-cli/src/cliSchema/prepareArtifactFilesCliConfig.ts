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
