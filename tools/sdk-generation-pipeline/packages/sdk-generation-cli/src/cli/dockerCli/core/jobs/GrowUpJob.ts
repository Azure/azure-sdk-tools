import { DockerContext } from '../DockerContext';
import { BaseJob } from './BaseJob';

export class GrowUpJob extends BaseJob {
    context: DockerContext;

    constructor(context: DockerContext) {
        super();
        this.context = context;
    }

    public async execute() {
        this.context.logger.info(`Please use vscode to connect this container.`);
        this.doNotExitDockerContainer();
    }
}
