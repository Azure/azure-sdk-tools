import { DockerTaskEngineContext, initializeDockerTaskEngineContext } from "../../src/cli/dockerCli/core/dockerTaskEngine";
import { DockerContext } from "../../src/cli/dockerCli/core/DockerContext";
import * as path from "path";
import { initializeLogger } from "@azure-tools/sdk-generation-lib";
import { runTaskEngine } from "../../dist/cli/dockerCli/core/dockerTaskEngine";
import { existsSync } from "fs";

describe('task engine', () => {
    it('should initialize a DockerTaskEngineContext by DockerContext', async () => {
        const dockerContext = new DockerContext();
        const tmpFolder = path.join(path.resolve('.'), 'test', 'unit', 'tmp');
        dockerContext.initialize({
            readmeMdPath: 'specification/agrifood/resource-manager/readme.md',
            tag: '',
            sdk: '',
            specRepo: path.join(tmpFolder, 'spec-repo'),
            workDir: '/work-dir',
            sdkRepo: path.join(tmpFolder, 'sdk-repo'),
            resultOutputFolder: path.join(tmpFolder, 'output'),
            dockerLogger: 'docker.log'
        });

        const dockerTaskEngineContext: DockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
        expect(dockerTaskEngineContext.configFilePath).toBe('eng/codegen_to_sdk_config.json');
        expect(dockerTaskEngineContext.initOutput).toBe(path.join(tmpFolder, 'output', 'initOutput.json'));
        expect(dockerTaskEngineContext.generateAndBuildInputJson).toBe(path.join(tmpFolder, 'output', 'generateAndBuildInput.json'));
        expect(dockerTaskEngineContext.generateAndBuildOutputJson).toBe(path.join(tmpFolder, 'output', 'generateAndBuildOutputJson.json'));
        expect(dockerTaskEngineContext.mockTestInputJson).toBe(path.join(tmpFolder, 'output', 'mockTestInput.json'));
        expect(dockerTaskEngineContext.mockTestOutputJson).toBe(path.join(tmpFolder, 'output', 'mockTestOutput.json'));
        expect(dockerTaskEngineContext.initTaskLog).toBe(path.join(tmpFolder, 'output', 'init-task.log'));
        expect(dockerTaskEngineContext.generateAndBuildTaskLog).toBe(path.join(tmpFolder, 'output', 'generate-and-build-task.log'));
        expect(dockerTaskEngineContext.mockTestTaskLog).toBe(path.join(tmpFolder, 'output', 'mock-test-task.log'));
        expect(dockerTaskEngineContext.readmeMdPath).toBe('specification/agrifood/resource-manager/readme.md');
    });

    it('should run tasks', async () => {
        jest.setTimeout(999999);
        const tmpFolder = path.join(path.resolve('.'), 'test', 'unit', 'tmp');
        const dockerTaskEngineContext: DockerTaskEngineContext = {
            sdkRepo: path.join(tmpFolder, 'sdk-repo'),
            taskResultJsonPath: path.join(tmpFolder, 'output', 'taskResults.json'),
            logger: initializeLogger(path.join(tmpFolder, 'docker.log'), 'docker', true),
            configFilePath: 'eng/codegen_to_sdk_config.json',
            initOutput: path.join(tmpFolder, 'output', 'initOutput.json'),
            generateAndBuildInputJson: path.join(tmpFolder, 'output', 'generateAndBuildInput.json'),
            generateAndBuildOutputJson: path.join(tmpFolder, 'output', 'generateAndBuildOutputJson.json'),
            mockTestInputJson: path.join(tmpFolder, 'output', 'mockTestInput.json'),
            mockTestOutputJson: path.join(tmpFolder, 'output', 'mockTestOutput.json'),
            initTaskLog: path.join(tmpFolder, 'output', 'init-task.log'),
            generateAndBuildTaskLog: path.join(tmpFolder, 'output', 'generate-and-build-task.log'),
            mockTestTaskLog: path.join(tmpFolder, 'output', 'mock-test-task.log'),
            readmeMdPath: 'specification/agrifood/resource-manager/readme.md',
            specRepo: {
                repoPath: path.join(tmpFolder, 'spec-repo'),
                headSha: '11111',
                headRef: '11111',
                repoHttpsUrl: 'https://github.com/Azure/azure-rest-api-specs'
            },
            changeOwner: false
        }
        await runTaskEngine(dockerTaskEngineContext);
        expect(existsSync(dockerTaskEngineContext.initTaskLog)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildInputJson)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildOutputJson)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.generateAndBuildTaskLog)).toBe(true);
        expect(existsSync(dockerTaskEngineContext.taskResultJsonPath)).toBe(true);
    });
});
