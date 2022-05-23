import { spawn } from 'child_process';
import { existsSync } from 'fs';
import * as path from 'path';

import { getChangedPackageDirectory } from '../../../../utils/git';
import { sdkToRepoMap } from '../constants';
import { DockerContext } from '../DockerContext';
import { DockerTaskEngineContext } from '../DockerTaskEngineContext';
import { BaseJob } from './BaseJob';

export class GenerateCodesInLocalJob extends BaseJob {
    context: DockerContext;

    constructor(context: DockerContext) {
        super();
        this.context = context;
    }

    public async cloneRepoIfNotExist(sdkRepos: string[]) {
        for (const sdkRepo of sdkRepos) {
            if (!existsSync(path.join(this.context.workDir, sdkRepo))) {
                const child = spawn(`git`, [`clone`, `https://github.com/Azure/${sdkRepo}.git`], {
                    cwd: this.context.workDir,
                    stdio: ['ignore', 'pipe', 'pipe']
                });
                child.stdout.on('data', (data) => this.context.logger.log('cmdout', data.toString()));
                child.stderr.on('data', (data) => this.context.logger.log('cmderr', data.toString()));
                await new Promise((resolve) => {
                    child.on('exit', (code, signal) => {
                        resolve({ code, signal });
                    });
                });
            }
            this.context.sdkRepo = path.join(this.context.workDir, sdkRepo);
        }
    }

    public async execute() {
        const sdkRepos: string[] = this.context.sdkList.map((ele) => sdkToRepoMap[ele]);
        await this.cloneRepoIfNotExist(sdkRepos);
        for (const sdk of this.context.sdkList) {
            this.context.sdkRepo = path.join(this.context.workDir, sdkToRepoMap[sdk]);
            const dockerTaskEngineContext = new DockerTaskEngineContext();
            dockerTaskEngineContext.initialize(this.context);
            await dockerTaskEngineContext.runTaskEngine();
        }

        const generatedCodesPath: Map<string, Set<string>> = new Map();

        for (const sdk of this.context.sdkList) {
            generatedCodesPath[sdk] = await getChangedPackageDirectory(path.join(this.context.workDir, sdkToRepoMap[sdk]));
        }

        this.context.logger.info(`Finish generating sdk for ${this.context.sdkList.join(', ')}.`);
        for (const sdk of this.context.sdkList) {
            if (generatedCodesPath[sdk].size > 0) {
                this.context.logger.info(`You can find generated ${sdk} codes in:`);
                generatedCodesPath[sdk].forEach((ele) => {
                    this.context.logger.info(`    - ${path.join(this.context.workDir, sdkToRepoMap[sdk], ele)}`);
                });
            }
        }
        this.context.logger.info(`You can use vscode to connect this docker container for further development.`);
        this.doNotExitDockerContainer();
    }
}
