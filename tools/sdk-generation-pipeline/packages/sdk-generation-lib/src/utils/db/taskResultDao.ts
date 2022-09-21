import { TaskResult } from '../../types/taskResult';

export interface TaskResultDao {
    getFromBuild(pipelineBuildId: string);
    put(pipelineBuildId: string, taskResult: TaskResult);
}
