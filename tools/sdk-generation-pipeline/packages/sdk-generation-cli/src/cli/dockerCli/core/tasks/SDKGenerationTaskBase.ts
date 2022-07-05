import { DockerTaskEngineContext } from '../DockerTaskEngineContext';

export type TaskType = 'InitTask' | 'GenerateAndBuildTask' | 'MockTestTask';

export interface SDKGenerationTaskBase {
    taskType: TaskType;
    context: DockerTaskEngineContext;
    order: number;
    execute();
}
