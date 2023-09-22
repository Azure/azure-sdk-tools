import { Connection, MongoRepository } from 'typeorm';

import { TaskResult, TaskResultEntity } from '../../types/taskResult';
import { TaskResultDao } from './taskResultDao';

export class TaskResultDaoImpl implements TaskResultDao {
    private repo: MongoRepository<TaskResultEntity>;

    constructor(connection: Connection) {
        this.repo = connection.getMongoRepository(TaskResultEntity);
    }

    public async getFromBuild(pipelineBuildId: string): Promise<TaskResult[]> {
        const taskResults: TaskResultEntity[] = await this.repo.find({
            pipelineBuildId: pipelineBuildId
        });
        const results: TaskResult[] = [];
        for (const taskResult of taskResults) {
            results.push(taskResult.taskResult);
        }
        return results;
    }

    public async put(pipelineBuildId: string, taskResult: TaskResult) {
        const key = `${pipelineBuildId}/${taskResult.name}`;
        await this.repo.findOneAndReplace(
            { key: key },
            {
                key: key,
                pipelineBuildId: pipelineBuildId,
                taskResult: taskResult
            },
            { upsert: true }
        );
    }
}
