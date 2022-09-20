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
        if (!this.context.sdkWorkBranchLink) return;
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

    private async cloneSpecRepoIfNotExist() {
        if (!this.context.specLink) return;
        if (fs.existsSync(this.context.specRepo)) {
            this.context.logger.info(`${this.context.specRepo} has already exited, and no need to clone it`);
            return;
        }
        const gitOperationWrapper = new GitOperationWrapper('/');
        const prLinkMatch = this.context.specLink.match(/http.*\/([^\/]*\/[^\/]*)\/pull\/([0-9]+)/);
        const branchLinkMatch = this.context.specLink.match(/(https.*\/[^\/]*)\/tree\/(.*)/);
        const mainBranchMatch = this.context.specLink.match(/http.*\/([^\/]*\/[^\/\.]*)/);
        if (prLinkMatch?.length === 3) {
            const repoName = prLinkMatch[1];
            const prNumber = prLinkMatch[2];
            await gitOperationWrapper.cloneRepo(repoName, this.context.logger, this.context.specRepo);
            gitOperationWrapper.changeBaseDir(this.context.specRepo);
            await gitOperationWrapper.safeDirectory();
            await gitOperationWrapper.checkoutPr(prNumber);
        } else if (branchLinkMatch?.length == 3) {
            const repoUrl = branchLinkMatch[1];
            const branch = branchLinkMatch[2];
            await gitOperationWrapper.cloneBranch(repoUrl, branch, this.context.logger, this.context.specRepo);
            gitOperationWrapper.changeBaseDir(this.context.specRepo);
            await gitOperationWrapper.safeDirectory();
        } else if (mainBranchMatch?.length == 2) {
            // spec repo main branch
            const repoName = mainBranchMatch[1];
            await gitOperationWrapper.cloneRepo(repoName, this.context.logger, this.context.specRepo);
            gitOperationWrapper.changeBaseDir(this.context.specRepo);
            await gitOperationWrapper.safeDirectory();
        } else {
            throw new Error(`Get invalid spec link: ${this.context.specLink}`);
        }
    }

    public async execute() {
        await this.cloneSpecRepoIfNotExist();
        await this.cloneSdkWorkBranchIfNotExist();
        this.context.logger.info(`Please use vscode to connect this container.`);
        this.doNotExitDockerContainer();
    }
}
