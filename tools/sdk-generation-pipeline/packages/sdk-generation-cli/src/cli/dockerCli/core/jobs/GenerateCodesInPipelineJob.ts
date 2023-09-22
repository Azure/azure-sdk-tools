import { DockerContext } from '../DockerContext';
import { DockerTaskEngineContext } from '../DockerTaskEngineContext';
import { BaseJob } from './BaseJob';

export class GenerateCodesInPipelineJob extends BaseJob {
    context: DockerContext;

    constructor(context: DockerContext) {
        super();
        this.context = context;
    }

    public async execute() {
        const context: DockerTaskEngineContext = new DockerTaskEngineContext();
        await context.initialize(this.context);
        await context.runTaskEngine();
    }
}
