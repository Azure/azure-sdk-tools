import convict from 'convict';
import * as dotenv from 'dotenv';

dotenv.config();

export class DockerMockHostInput {
    mockHostLogger: string;
    mockHostPath: string;
}

export const dockerMockHostInput = convict<DockerMockHostInput>({
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
