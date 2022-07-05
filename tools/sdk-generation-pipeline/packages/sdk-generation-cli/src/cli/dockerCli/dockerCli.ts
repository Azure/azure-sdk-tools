#!/usr/bin/env node
import { DockerContext } from './core/DockerContext';
import { DockerRunningModel } from './core/DockerRunningModel';
import { GenerateCodesInLocalJob } from './core/jobs/GenerateCodesInLocalJob';
import { GenerateCodesInPipelineJob } from './core/jobs/GenerateCodesInPipelineJob';
import { GrowUpJob } from './core/jobs/GrowUpJob';
import { DockerCliInput, dockerCliInput } from './schema/dockerCliInput';

async function main() {
    const inputParams: DockerCliInput = dockerCliInput.getProperties();
    const context: DockerContext = new DockerContext();
    context.initialize(inputParams);

    let executeJob: GenerateCodesInLocalJob | GrowUpJob | GenerateCodesInPipelineJob;

    switch (context.mode) {
    case DockerRunningModel.CodeGenAndGrowUp:
        executeJob = new GenerateCodesInLocalJob(context);
        break;
    case DockerRunningModel.GrowUp:
        executeJob = new GrowUpJob(context);
        break;
    case DockerRunningModel.Pipeline:
        executeJob = new GenerateCodesInPipelineJob(context);
        break;
    }

    if (!!executeJob) {
        await executeJob.execute();
    }
}

main().catch((e) => {
    console.error('\x1b[31m', e.toString());
    console.error('\x1b[31m', e.message);
    console.error('\x1b[31m', e.stack);
    process.exit(1);
});
