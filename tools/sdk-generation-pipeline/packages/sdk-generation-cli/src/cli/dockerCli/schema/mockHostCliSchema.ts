import convict from 'convict';
import * as dotenv from 'dotenv';

dotenv.config();

export class MockHostCliSchema {
    readmeMdPath: string;
    specRepo: string;
    resultOutputFolder: string;
    mockHostLogger: string;
    mockHostPath: string;
}

export const mockHostCliSchema = convict<MockHostCliSchema>({
    readmeMdPath: {
        default: '',
        env: 'README_MD_PATH',
        arg: 'readme',
        format: String,
        doc: 'The relative path to readme.md, which is from the root of spec repo'
    },
    specRepo: {
        default: '/spec-repo',
        env: 'SPEC_REPO',
        arg: 'spec-repo',
        format: String,
        doc: 'the absolute path of the mounted spec repo'
    },
    resultOutputFolder: {
        default: '/tmp/output',
        env: 'RESULT_OUTPUT_FOLDER',
        arg: 'result-output-folder',
        format: String,
        doc: 'the absolute path of output folder, which stores the result of task engine'
    },
    mockHostLogger: {
        default: 'mock-host.log',
        env: 'MOCK_HOST_LOGGER',
        arg: 'mock-host-logger',
        format: String,
        doc: 'the path of mock-host.log. it will concat with resultOutputFolder'
    },
    mockHostPath: {
        default: '/mock-host',
        env: 'MOCK_HOST_PATH',
        arg: 'mock-host-path',
        format: String,
        doc: 'the path of mock-host'
    }
});
