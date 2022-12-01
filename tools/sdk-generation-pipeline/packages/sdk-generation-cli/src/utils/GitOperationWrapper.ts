import { spawn } from 'child_process';
import * as os from 'os';
import simpleGit, { SimpleGit } from 'simple-git';
import { Logger } from 'winston';

export class GitOperationWrapper {
    public git: SimpleGit;
    public baseDir: string;

    constructor(baseDir: string = '.') {
        this.baseDir = baseDir;
        this.git = simpleGit({ baseDir: baseDir });
    }

    public async getFileListInPackageFolder(packageFolder: string) {
        const files = (await this.git.raw(['ls-files', '-cmo', '--exclude-standard']))?.trim()?.split(os.EOL);
        return files;
    }

    public async getHeadSha() {
        const headSha = await this.git.revparse(['HEAD']);
        return headSha;
    }

    public async getHeadRef() {
        const headRef = await this.git.revparse(['--abbrev-ref', 'HEAD']);
        return headRef;
    }

    public async getRemote() {
        const remote = await this.git.getConfig('remote.origin.url');
        return remote?.value?.replace('.git', '');
    }

    public async safeDirectory() {
        await this.git.addConfig('safe.directory', this.baseDir, true, 'global');
    }

    public async disableFileMode() {
        await this.git.raw(['config', 'core.fileMode', 'false', '--replace-all']);
    }

    public async checkoutPr(prNumber: string) {
        await this.git.raw(['fetch', 'origin', `pull/${prNumber}/head`]);
        await this.git.raw(['checkout', '-B', `pr-${prNumber}`, `FETCH_HEAD`]);
    }

    public async getChangedPackageDirectory(): Promise<Set<string>> {
        const changedPackageDirectories: Set<string> = new Set<string>();
        const files = (await this.git.raw(['ls-files', '-mdo', '--exclude-standard'])).trim().split(os.EOL);
        for (const filePath of files) {
            if (filePath.match(/sdk\/[^\/0-9]*\/.*/)) {
                const packageDirectory = /sdk\/[^\/0-9]*\/[^\/]*/.exec(filePath);
                if (packageDirectory) {
                    changedPackageDirectories.add(packageDirectory[0]);
                }
            }
        }
        return changedPackageDirectories;
    }

    public async cloneRepo(githubRepo: string, logger: Logger, repoAlias?: string) {
        const additionalParams = [];
        if (!!repoAlias) additionalParams.push(repoAlias);
        const child = spawn(`git`, [`clone`, `https://github.com/${githubRepo}.git`, ...additionalParams], {
            cwd: this.baseDir,
            stdio: ['ignore', 'pipe', 'pipe']
        });
        await this.injectListener(child, logger);
    }

    private async injectListener(child: any, logger: Logger) {
        child.stdout.on('data', (data) => logger.log('cmdout', data.toString()));
        child.stderr.on('data', (data) => logger.log('cmdout', data.toString()));
        await new Promise((resolve) => {
            child.on('exit', (code, signal) => {
                resolve({ code, signal });
            });
        });
    }

    public async cloneBranch(repoUrl: string, branchName: string, logger: Logger, repoAlias?: string) {
        const additionalParams = [];
        if (!!repoAlias) additionalParams.push(repoAlias);
        const child = spawn(`git`, [`clone`, '--branch', branchName, repoUrl, ...additionalParams], {
            cwd: this.baseDir,
            stdio: ['ignore', 'pipe', 'pipe']
        });
        await this.injectListener(child, logger);
    }

    public changeBaseDir(baseDir: string) {
        this.baseDir = baseDir;
        this.git = simpleGit({ baseDir: baseDir });
    }
}
