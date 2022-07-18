import * as fs from 'fs';
import * as path from 'path';

import { GitOperationWrapper } from '../../../../utils/GitOperationWrapper';
import { DockerContext } from '../DockerContext';
import { BaseJob } from './BaseJob';

export class GrowUpJob extends BaseJob {
    context: DockerContext;

    constructor(context: DockerContext) {
        super();
        this.context = context;
    }

    private async cloneSdkWorkBranchIfNotExist() {
        if (!!this.context.sdkWorkBranchLink) {
            const match = this.context.sdkWorkBranchLink.match(/(https.*\/([^\/]*))\/tree\/(.*)/);
            const sdkRepoUrl = match[1];
            const sdkRepo = match[2];
            const branch = match[3];
            if (fs.existsSync(path.join(this.context.workDir, sdkRepo))) {
                this.context.logger.info(`${path.join(this.context.workDir, sdkRepo)} has already existed, and no need to clone it.`);
                return;
            }
            const gitOperationWrapper = new GitOperationWrapper(this.context.workDir);
            await gitOperationWrapper.cloneBranch(sdkRepoUrl, branch, this.context.logger);
            gitOperationWrapper.baseDir = path.join(this.context.workDir, sdkRepo);
            await gitOperationWrapper.safeDirectory();
        }
    }

    public async execute() {
        await this.cloneSdkWorkBranchIfNotExist();
        this.context.logger.info(`Please use vscode to connect this container.`);
        this.doNotExitDockerContainer();
    }
}
