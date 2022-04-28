import { DockerTaskEngineContext, initializeDockerTaskEngineContext } from "../src/cli/dockerCli/core/dockerTaskEngine";
import { DockerContext } from "../src/cli/dockerCli/core/DockerContext";
import * as path from "path";

describe('task engine', () => {
    it('should initialize a DockerTaskEngineContext by DockerContext', async () => {
        const dockerContext = new DockerContext();
        const tmpFolder = path.join(path.resolve('.'), 'tmp');
        dockerContext.initialize({
            readmeMdPath: 'specification/agrifood/resource-manager/readme.md',
            tag: '',
            sdkToGenerate: '',
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
});