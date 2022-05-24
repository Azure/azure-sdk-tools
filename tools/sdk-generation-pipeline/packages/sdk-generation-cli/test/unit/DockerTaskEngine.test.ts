import { initializeLogger } from '@azure-tools/sdk-generation-lib';
import { existsSync } from 'fs';
import * as path from 'path';

import { DockerContext } from '../../src/cli/dockerCli/core/DockerContext';
import {
    DockerTaskEngineContext
} from '../../src/cli/dockerCli/core/DockerTaskEngineContext';
import { SDKGenerationTaskBase } from '../../src/cli/dockerCli/core/tasks/SDKGenerationTaskBase';

describe('task engine', () => {
    it('should initialize a DockerTaskEngineContext by DockerContext', async () => {
        const dockerContext = new DockerContext();
        const tmpFolder = path.join(path.resolve('.'), 'test', 'unit', 'tmp');
        dockerContext.initialize({
            readmeMdPath: 'specification/agrifood/resource-manager/readme.md',
            tag: '',
            sdkList: '',
            specRepo: path.join(tmpFolder, 'spec-repo'),
            workDir: '/work-dir',
            sdkRepo: path.join(tmpFolder, 'sdk-repo'),
            resultOutputFolder: path.join(tmpFolder, 'output'),
            dockerLogger: 'docker.log'
        });
        const dockerTaskEngineContext = new DockerTaskEngineContext();
        dockerTaskEngineContext.initialize(dockerContext);
        expect(dockerTaskEngineContext.configFilePath).toBe('eng/codegen_to_sdk_config.json');
        expect(dockerTaskEngineContext.initOutput).toBe(path.join(tmpFolder, 'output', 'initOutput.json'));
        expect(dockerTaskEngineContext.generateAndBuildInputJson).toBe(path.join(tmpFolder, 'output', 'generateAndBuildInput.json'));
        expect(dockerTaskEngineContext.generateAndBuildOutputJson).toBe(path.join(tmpFolder, 'output', 'generateAndBuildOutputJson.json'));
        expect(dockerTaskEngineContext.mockTestInputJson).toBe(path.join(tmpFolder, 'output', 'mockTestInput.json'));
        expect(dockerTaskEngineContext.mockTestOutputJson).toBe(path.join(tmpFolder, 'output', 'mockTestOutput.json'));
        expect(dockerTaskEngineContext.initTaskLog).toBe(path.join(tmpFolder, 'output', 'init-task.log'));
        expect(dockerTaskEngineContext.generateAndBuildTaskLog).toBe(path.join(tmpFolder, 'output', 'generateAndBuild-task.log'));
        expect(dockerTaskEngineContext.mockTestTaskLog).toBe(path.join(tmpFolder, 'output', 'mockTest-task.log'));
        expect(dockerTaskEngineContext.readmeMdPath).toBe('specification/agrifood/resource-manager/readme.md');
    });

    it('should get task list', async () => {
        const tmpFolder = path.join(path.resolve('.'), 'test', 'unit', 'tmp');
        const dockerTaskEngineContext = new DockerTaskEngineContext();
        dockerTaskEngineContext.sdkRepo = path.join(tmpFolder, 'sdk-repo');
        dockerTaskEngineContext.configFilePath = 'eng/codegen_to_sdk_config.json';
        dockerTaskEngineContext.logger = initializeLogger(path.join(tmpFolder, 'docker.log'), 'docker', true);
        const tasksToRun: SDKGenerationTaskBase[] = await dockerTaskEngineContext.getTaskToRun();
        expect(tasksToRun.length).toEqual(2);
        expect(tasksToRun[0].taskType).toEqual('InitTask');
        expect(tasksToRun[1].taskType).toEqual('GenerateAndBuildTask');
    });

    it('should run tasks', async () => {
        jest.setTimeout(999999);
        const tmpFolder = path.join(path.resolve('.'), 'test', 'unit', 'tmp');
        const dockerTaskEngineContext = new DockerTaskEngineContext();

        dockerTaskEngineContext.sdkRepo = path.join(tmpFolder, 'sdk-repo');
        dockerTaskEngineContext.taskResultJsonPath = path.join(tmpFolder, 'output', 'taskResults.json');
        dockerTaskEngineContext.logger = initializeLogger(path.join(tmpFolder, 'docker.log'), 'docker', true);
        dockerTaskEngineContext.configFilePath = 'eng/codegen_to_sdk_config.json';
        dockerTaskEngineContext.initOutput = path.join(tmpFolder, 'output', 'initOutput.json');
        dockerTaskEngineContext.generateAndBuildInputJson = path.join(tmpFolder, 'output', 'generateAndBuildInput.json');
        dockerTaskEngineContext.generateAndBuildOutputJson = path.join(tmpFolder, 'output', 'generateAndBuildOutputJson.json');
        dockerTaskEngineContext.mockTestInputJson = path.join(tmpFolder, 'output', 'mockTestInput.json');
        dockerTaskEngineContext.mockTestOutputJson = path.join(tmpFolder, 'output', 'mockTestOutput.json');
        dockerTaskEngineContext.initTaskLog = path.join(tmpFolder, 'output', 'init-task.log');
        dockerTaskEngineContext.generateAndBuildTaskLog = path.join(tmpFolder, 'output', 'generate-and-build-task.log');
        dockerTaskEngineContext.mockTestTaskLog = path.join(tmpFolder, 'output', 'mock-test-task.log');
        dockerTaskEngineContext.readmeMdPath = 'specification/agrifood/resource-manager/readme.md';
        dockerTaskEngineContext.specRepo = {
            repoPath: path.join(tmpFolder, 'spec-repo'),
            headSha: '11111',
            headRef: '11111',
            repoHttpsUrl: 'https://github.com/Azure/azure-rest-api-specs'
        };
        dockerTaskEngineContext.changeOwner = false;

        await dockerTaskEngineContext.runTaskEngine();
        expect(existsSync(dockerTaskEngineContext.initTaskLog)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildInputJson)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildOutputJson)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildTaskLog)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.taskResultJsonPath)).toBe(true);
    });
});
