import commandLineArgs from 'command-line-args'
import { Octokit } from '@octokit/rest'

async function listAllPrs(octokit, owner: string, repo: string) {
    const allPrs = await octokit.paginate(`Get /repos/{owner}/{repo}/pulls`, {
        owner: owner,
        repo: repo
    },
        response => response.data);
    return allPrs;
}

function initializeOctokit(authToken: string) {
    return new Octokit({
        auth: authToken.trim()
    });
}

function splitRepoInfo(repoInfo: string) {
    const repoInfoSplits = repoInfo.split('/');
    return {
        owner: repoInfoSplits[0],
        repo: repoInfoSplits[1]
    }
}

async function swaggerPrIsClosed(octokit: Octokit, body: string) {
    if (!body) return false;
    const match = /https:\/\/github\.com\/([^/]+)\/([^/]+)\/pull\/([0-9]+)/gm.exec(body);
    if (!match || match.length != 4) return false;
    const owner = match[1];
    const repo = match[2];
    const prNumber = parseInt(match[3]);
    const swaggerPr = await octokit.rest.pulls.get({
        owner: owner,
        repo: repo,
        pull_number: prNumber
    });
    return swaggerPr?.data?.state === 'closed';
}

async function closePR(octokit: Octokit, owner: string, repo: string, prNumber: number, htmlUrl: string) {
    console.log(`close PR: ${owner}/${repo}: ${htmlUrl}`);
    await octokit.pulls.update({
        owner: owner,
        repo: repo,
        pull_number: prNumber,
        state: 'closed'
    });
}

async function deleteBranch(octokit: Octokit, owner: string, repo: string, branchName: string) {
    console.log(`delete branch: ${owner}/${repo}: ${branchName}`);
    await octokit.git.deleteRef({
        owner: owner,
        repo: repo,
        ref: `heads/${branchName}`
    });
}

async function cleanupBranch(repoInfo: string, branchPrefix: string, prCreator: string, authToken: string) {
    const octokit: Octokit = initializeOctokit(authToken);
    const {owner, repo} = splitRepoInfo(repoInfo);
    const allPrs = await listAllPrs(octokit, owner, repo);
    for (const pr of allPrs) {
        if (pr.user.login === prCreator && pr.head.ref.startsWith(branchPrefix)) {
            if (await swaggerPrIsClosed(octokit, pr.body)) {
                if (pr.state === 'open') {
                    await closePR(octokit, owner, repo, pr.number, pr.html_url);
                }
                await deleteBranch(octokit, owner, repo, pr.head.ref);
            } else {
                console.log(`skip processing ${pr.html_url} because corresponding swagger PR is not closed/merged or linked`);
            }
        }
    }
}

const optionDefinitions = [
    {name: 'repo', type: String},
    {name: 'branch-prefix', type: String},
    {name: 'pr-creator', type: String},
    {name: 'auth-token', type: String},
];

const options = commandLineArgs(optionDefinitions);
cleanupBranch(options['repo'], options['branch-prefix'], options['pr-creator'], options['auth-token']).catch(e => {
    console.log(e);
    process.exit(1);
})