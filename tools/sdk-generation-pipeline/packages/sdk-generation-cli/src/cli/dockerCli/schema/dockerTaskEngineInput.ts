import convict from 'convict';
import * as dotenv from 'dotenv';

dotenv.config();

export class DockerTaskEngineInput {
    configFilePath: string;
    initOutputJsonFile: string;
    generateAndBuildInputJsonFile: string;
    generateAndBuildOutputJsonFile: string;
    mockTestInputJsonFile: string;
    mockTestOutputJsonFile: string;
    headSha: string | undefined;
    headRef: string | undefined;
    repoHttpsUrl: string | undefined;
    serviceType: string;
    mockServerHost?: string;
    initTaskLog: string;
    generateAndBuildTaskLog: string;
    mockTestTaskLog: string;
    taskResultJson: string;
    changeOwner: boolean;
}

export const dockerTaskEngineInput = convict<DockerTaskEngineInput>({
    configFilePath: {
        default: 'eng/codegen_to_sdk_config.json',
        env: 'CONFIG_FILE_PATH',
        arg: 'configFilePath',
        format: String,
        doc: 'The relative path to codegen_to_sdk_config.json'
    },
    initOutputJsonFile: {
        default: 'initOutput.json',
        env: 'INIT_OUTPUT_JSON_FILE',
        arg: 'initOutputJsonFile',
        format: String,
        doc: 'The relative path to initOut.json. It will concat with resultOutputFolder'
    },
    generateAndBuildInputJsonFile: {
        default: 'generateAndBuildInput.json',
        env: 'GENERATE_AND_BUILD_INPUT_JSON_FILE',
        arg: 'generateAndBuildInputJsonFile',
        format: String,
        doc: 'The relative path to generateAndBuildInput.json. It will concat with resultOutputFolder'
    },
    generateAndBuildOutputJsonFile: {
        default: 'generateAndBuildOutputJson.json',
        env: 'GENERATE_AND_BUILD_OUTPUT_JSON_File',
        arg: 'generateAndBuildOutputJsonFile',
        format: String,
        doc: 'The relative path to generateAndBuildOutput.json. It will concat with resultOutputFolder'
    },
    mockTestInputJsonFile: {
        default: 'mockTestInput.json',
        env: 'MOCK_TEST_INPUT_JSON_FILE',
        arg: 'mockTestInputJsonFile',
        format: String,
        doc: 'The relative path to mockTestInput.json. It will concat with resultOutputFolder'
    },
    mockTestOutputJsonFile: {
        default: 'mockTestOutput.json',
        env: 'MOCK_TEST_OUTPUT_JSON_FILE',
        arg: 'mockTestOutputJsonFile',
        format: String,
        doc: 'The relative path to mockTestOutput.json. It will concat with resultOutputFolder'
    },
    headSha: {
        default: undefined,
        env: 'HEAD_SHA',
        format: String,
        doc: 'headSha of spec repo'
    },
    headRef: {
        default: undefined,
        env: 'HEAD_REF',
        format: String,
        doc: 'headRef of spec repo'
    },
    repoHttpsUrl: {
        default: undefined,
        env: 'REPO_HTTP_URL',
        format: String,
        doc: 'The http url of spec repo'
    },
    serviceType: {
        default: 'resource-manager',
        env: 'SERVICE_TYPE',
        format: String,
        doc: 'resource-manager or data-plane'
    },
    mockServerHost: {
        default: 'https://localhost:8443',
        env: 'MOCK_SERVER_HOST',
        format: String,
        doc: 'The host of mocker server'
    },
    initTaskLog: {
        default: 'init-task.log',
        env: 'INIT_TASK_LOG',
        format: String,
        doc: 'The relative path to init-task.log. It will concat with resultOutputFolder'
    },
    generateAndBuildTaskLog: {
        default: 'generateAndBuild-task.log',
        env: 'GENERATE_AND_BUILD_TASK_LOG',
        format: String,
        doc: 'The relative path to generate-and-build-task.log. It will concat with resultOutputFolder'
    },
    mockTestTaskLog: {
        default: 'mockTest-task.log',
        env: 'MOCK_TEST_TASK_LOG',
        format: String,
        doc: 'The relative path to mock-test-task.log. It will concat with resultOutputFolder'
    },
    taskResultJson: {
        default: 'taskResults.json',
        env: 'TASK_RESULT_JSON',
        format: String,
        doc: 'The relative path to taskResult.json. It will concat with resultOutputFolder'
    },
    changeOwner: {
        default: true,
        env: 'CHANGE_OWNER',
        format: Boolean,
        doc: 'When the commands run in docker, it is required to change the sdk owner because generated codes is owned by root'
    }
});
