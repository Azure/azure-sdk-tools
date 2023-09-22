#!/usr/bin/env node
import { logger } from '@azure-tools/sdk-generation-lib';
import * as path from 'path';

async function main(repoKey: string, repoUrl: string) {
    repoUrl = repoUrl.replace('.git', '');
    if (repoUrl.endsWith('/')) {
        repoUrl = repoUrl.substring(0, repoUrl.length - 1);
    }
    const repoUrlSplit = repoUrl.split('/');
    const repoName = repoUrlSplit[repoUrlSplit.length - 1];
    console.log(`##vso[task.setVariable variable=${repoKey}]${path.join(path.resolve('.'), repoName)}`);
}

const repoUrl = process.argv.pop();
const repoKey = process.argv.pop();
main(repoKey, repoUrl).catch((e) => {
    logger.error(`${e.message}
    ${e.stack}`);
});
