import { execSync } from 'child_process';
import commandLineArgs from 'command-line-args';
import { existsSync, mkdirSync } from 'fs';
import * as path from 'path';
import * as process from 'process';

const repoCommitId = {
    'azure-rest-api-specs': '0baca05c851c1749e92beb0d2134cd958827dd54',
    'azure-sdk-for-js': '57382229a700e0e6f607d6ac0811379a6254f3d9',
    'azure-sdk-for-java': '307df24267304fbf3947025bef7eaf9698410de8',
    'azure-sdk-for-python': '53f66170cc47739204cedfe0a46989290c047c98',
    'azure-sdk-for-go': '241bdb849ce431e1a5e398a5649cde93149ee374',
    'azure-sdk-for-net': 'e9db0733a642d50c34101339f74fdc487599d824'
};

const defaultImageName = 'sdkgeneration.azurecr.io/sdk-generation:v1.0';
const integrationBranch = 'sdkgeneration-integration-test';

async function prepareRepo(currentPath: string, repoName: string) {
    const tmpFolder = path.join(currentPath, 'tmp');
    if (!existsSync(tmpFolder)) {
        mkdirSync(tmpFolder);
    }

    if (!existsSync(path.join(tmpFolder, repoName))) {
        execSync(`git clone https://github.com/Azure/${repoName}.git`, {
            cwd: tmpFolder,
            stdio: 'inherit'
        });
    }
    execSync(`git restore --staged . && git restore . && git checkout . && git clean -fd`, {
        cwd: path.join(tmpFolder, repoName),
        stdio: 'inherit'
    });

    if (
        !!repoCommitId[repoName] &&
        execSync(`git rev-parse HEAD`, {
            encoding: 'utf-8',
            cwd: path.join(tmpFolder, repoName)
        }).trim() !== repoCommitId[repoName]
    ) {
        execSync(`git checkout ${repoCommitId[repoName]}`, {
            cwd: path.join(tmpFolder, repoName),
            stdio: 'inherit'
        });
    }

    if (
        execSync(`git rev-parse --abbrev-ref HEAD`, {
            encoding: 'utf-8',
            cwd: path.join(tmpFolder, repoName)
        }).trim() !== integrationBranch
    ) {
        execSync(`git switch -c ${integrationBranch}`, {
            cwd: path.join(tmpFolder, repoName),
            stdio: 'inherit'
        });
    }
}

async function runDocker(currentPath: string, sdkRepoName: string, dockerImage: string) {
    const tmpFolder = path.join(currentPath, 'tmp');
    // eslint-disable-next-line max-len
    execSync(
        `docker run -v ${path.join(tmpFolder, 'azure-rest-api-specs')}:/spec-repo -v ${path.join(
            tmpFolder,
            sdkRepoName
        )}:/sdk-repo ${dockerImage} --readme=specification/agrifood/resource-manager/readme.md`,
        {
            stdio: 'inherit'
        }
    );
}

async function buildDockImage(rushCwd: string, dockerCwd: string) {
    execSync(`rushx pack`, {
        cwd: rushCwd,
        stdio: 'inherit'
    });
    execSync(`docker build -t ${defaultImageName} .`, {
        cwd: dockerCwd,
        stdio: 'inherit'
    });
}

export async function main(options: any) {
    const currentPath = path.resolve(__dirname);
    if (!options['docker-image']) {
        await buildDockImage(path.join(currentPath, '..', '..'), path.join(currentPath, '..', '..', '..', '..'));
        options['docker-image'] = defaultImageName;
    }
    if (!options['sdk-repo']) {
        options['sdk-repo'] = Object.keys(repoCommitId)
            .filter((ele) => ele !== 'azure-rest-api-specs')
            .join(',');
    }
    await prepareRepo(currentPath, 'azure-rest-api-specs');
    for (const sdkRepo of options['sdk-repo'].split(',')) {
        await prepareRepo(currentPath, sdkRepo);
        await runDocker(currentPath, sdkRepo, options['docker-image']);
    }
}

const optionDefinitions = [
    { name: 'docker-image', type: String },
    { name: 'sdk-repo', type: String }
];
const options = commandLineArgs(optionDefinitions);

main(options).catch((err) => {
    console.log(err);
    process.exit(1);
});
