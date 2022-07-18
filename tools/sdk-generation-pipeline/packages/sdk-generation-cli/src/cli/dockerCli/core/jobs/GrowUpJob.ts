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

    private async cloneSpecRepoByGhIfNotExist() {
        if (!fs.existsSync(this.context.specRepo)) {
            const specPRLink = this.context.specPrLink;
            const match = specPRLink.match(/(https.*)\/pull\/(.*)/);
            const specRepoLink = match[1];
            const prNumber = match[2];
            const gitOperationWrapper = new GitOperationWrapper('/');
            await gitOperationWrapper.cloneRepo(specRepoLink, this.context.logger, this.context.specRepo);
            gitOperationWrapper.baseDir = this.context.specRepo;
            await gitOperationWrapper.checkoutPrRef(prNumber, this.context.logger);
        }
    }

    private async cloneSdkWorkBranchIfNotExist() {
        if (!!this.context.sdkWorkBranchLink) {
            const match = this.context.sdkWorkBranchLink.match(/(https.*\/([^\/]*))\/tree\/(.*)/);
            const sdkRepoUrl = match[1];
            const sdkRepo = match[2];
            const branch = match[3];
            if (fs.existsSync(path.join(this.context.workDir, sdkRepo))) {
                this.context.logger.info(`${path.join(this.context.workDir, sdkRepo)} has already existed, and no need to clone it.`)
                return;
            }
            const gitOperationWrapper = new GitOperationWrapper(this.context.workDir);
            await gitOperationWrapper.cloneBranch(sdkRepoUrl, branch, this.context.logger);
        }
    }

    public async execute() {
        await this.cloneSpecRepoByGhIfNotExist();
        await this.cloneSdkWorkBranchIfNotExist();
        this.context.logger.info(`Please use vscode to connect this container.`);
        this.doNotExitDockerContainer();
    }
}
