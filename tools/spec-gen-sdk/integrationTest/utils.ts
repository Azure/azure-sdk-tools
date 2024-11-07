import * as fs from 'fs-extra';
import _rimraf from 'rimraf';
import { Octokit } from '@octokit/rest';
import { sdkAutomationCliConfig } from '../src/cli/config';
import { getAuthenticatedOctokit } from '../src/utils/githubUtils';
import { deleteFolder } from '@ts-common/azure-js-dev-tools';
import * as winston from 'winston';
import path from 'path';
import simpleGit, { ResetMode, SimpleGit, SimpleGitOptions } from 'simple-git';
import { getCurrentBranch, gitAddAll, gitGetCommitter, gitGetDiffFileList, gitSetRemoteWithAuth } from '../src/utils/gitUtils';
import { sdkAutoLogLevels } from '../src/automation/logging';
import { simpleGitOptions } from '../src/automation/workflow';

const fixturePrefix = 'integrationTest/fixtures';
export const workPrefix = sdkAutomationCliConfig.workingFolder;
export const repoOwner = sdkAutomationCliConfig.specRepo.split('/')[0];

let runId = sdkAutomationCliConfig.testRunId;
if (!runId) {
  runId = Math.random().toString(36).substring(2, 8);
}
export const getRunIdPrefix = () => `test-${runId}`;
const logger = winston.createLogger({
  levels: sdkAutoLogLevels.levels,
  transports: [new winston.transports.Console({ level: 'error' })]
});
const [githubClient, getGithubAccessToken] = getAuthenticatedOctokit(
  {
    token: sdkAutomationCliConfig.githubToken,
    id: sdkAutomationCliConfig.githubApp.id,
    privateKey: sdkAutomationCliConfig.githubApp.privateKey
  },
  logger
);

export const getTestGithubClient = async (): Promise<Octokit> => {
  return githubClient;
};

export const getTestGitClient = async (repoName: string) => {
  const repoFolder = path.join(workPrefix, repoName);
  fs.mkdirpSync(repoFolder);

  const repo = simpleGit({ ...simpleGitOptions, baseDir: repoFolder });
  await repo.init(false);
  return repo;
};

export const cleanTestRepos = async (repos: string[]) => {
  const github = await getTestGithubClient();

  for (const repoName of repos) {
    console.log(`Cleaning up ${repoOwner}/${repoName}`);
    await deleteFolder(`${workPrefix}/${repoName}`);
    try {
      await github.repos.delete({ owner: repoOwner, repo: repoName });
    } catch (e) {
      console.log(`Failed to delete ${repoOwner}/${repoName}: ${e.message} ${e.stack}`);
    }
  }
};

type ReplaceFileRecord = {
  [filePath: string]: {
    search: string;
    replace: string;
  }[];
};
export const applyReplaceFileRecord = (workPath: string, toReplace?: ReplaceFileRecord) => {
  if (toReplace !== undefined) {
    for (const [filePath, replaceArr] of Object.entries(toReplace)) {
      let content = fs.readFileSync(`${workPath}/${filePath}`).toString();
      for (const { search, replace } of replaceArr) {
        content = content.replace(search, replace);
      }
      fs.writeFileSync(`${workPath}/${filePath}`, content);
    }
  }
};

export const initializeGithubRepoFromLocalFixture = async (
  fixtureName: string,
  repoName: string,
  toReplace?: ReplaceFileRecord,
  baseBranch: string = 'master'
) => {
  console.log(`Initializing https://github.com/${repoOwner}/${repoName}`);
  const fixturePath = path.join(fixturePrefix, fixtureName);
  const workPath = path.join(workPrefix, repoName);
  const github = await getTestGithubClient();

  // Copy file
  await fs.remove(workPath);
  await fs.mkdirp(workPath);
  await fs.copy(fixturePath, workPath);
  applyReplaceFileRecord(workPath, toReplace);
  const repo: SimpleGit = await getTestGitClient(repoName);

  // Create repo if not exist
  try {
    await github.repos.delete({ owner: repoOwner, repo: repoName });
  } catch (e) {
    // Repo not found. Pass
  }
  await github.repos.createInOrg({ name: repoName, org: repoOwner });
  try {
    await github.repos.get({ owner: repoOwner, repo: repoName });
  } catch (e) {}

  // Checkout branch
  await gitGetCommitter(repo);
  await repo.add([]).commit(`Init repo`)
  const commit = await repo.revparse('HEAD');
  const currentBranch = await getCurrentBranch(repo);
  if (baseBranch !== currentBranch) {
    await repo.raw(['branch', '--copy', commit, baseBranch, '--force']);
    await repo.checkoutLocalBranch(baseBranch);
  }

  // Add and commit
  await gitAddAll(repo);
  const diff = await repo.diff(['--name-status', 'HEAD']);
  let fileList = await gitGetDiffFileList(diff);
  await repo.add(fileList).commit(`Init repo with fixture ${fixtureName}`)

  // Push
  await gitSetRemoteWithAuth({ logger, getGithubAccessToken }, repo, 'origin', { owner: repoOwner, name: repoName });
  await repo.push([
    'origin',
    `+refs/heads/${baseBranch}:refs/heads/${baseBranch}`
  ]);
  console.log(`Initialized https://github.com/${repoOwner}/${repoName}`);
};

export const createPRWithPatch = async (
  repoName: string,
  patchName: string,
  branchName: string,
  options: {
    toReplace?: ReplaceFileRecord;
    baseBranch?: string;
    fetchBase?: boolean;
  } = {}
) => {
  const baseBranch = options.baseBranch ?? 'master';

  console.log(`Creating PR in '${repoOwner}/${repoName}' with patch '${patchName}' on branch '${branchName}'`);
  const workPath = `${workPrefix}/${repoName}`;
  const patchPath = `${fixturePrefix}/${patchName}`;
  const github = await getTestGithubClient();
  const repo = await getTestGitClient(repoName);

  await gitAddAll(repo);
  const header = await repo.revparse('HEAD')
  await repo.reset(ResetMode.HARD, [header]);
  await repo.checkout(header, ['--force']);

  if (options.fetchBase) {
    await repo.fetch([
       'origin',
       '--update-head-ok',
       '--force',
       `+refs/heads/${baseBranch}:refs/remotes/origin/${baseBranch}`
     ]);
    await repo.mergeFromTo(`origin/${baseBranch}`, baseBranch);
  }

  await repo.checkout(branchName, ['--force']);
  const baseCommit = await repo.revparse('HEAD');
  await repo.raw(['branch', '--copy', baseCommit, baseBranch, '--force']);
  await repo.checkout(branchName, ['--force']);

  await fs.copy(patchPath, workPath);
  applyReplaceFileRecord(workPath, options.toReplace);

  await gitAddAll(repo);
  const diff = await repo.diff(['--name-status', 'HEAD']);
  let fileList = await gitGetDiffFileList(diff);
  await gitGetCommitter(repo);
  await repo.add(fileList).commit(`Apply patch ${patchName}`)

  // Push
  await gitSetRemoteWithAuth({ logger, getGithubAccessToken }, repo, 'origin', { owner: repoOwner, name: repoName });
  await repo.push([
    'origin',
    `+refs/heads/${branchName}:refs/heads/${branchName}`
  ]);

  let pr = await github.pulls.create({
    owner: repoOwner,
    repo: repoName,
    head: branchName,
    base: baseBranch,
    title: branchName
  });
  console.log(pr.data.html_url);

  // Wait for mergable data
  while (pr.data.mergeable === null) {
    pr = await github.pulls.get({ owner: repoOwner, repo: repoName, pull_number: pr.data.number });
  }

  return pr.data.number;
};

export const getPRListInfo = async (repoName: string) => {
  const github = await getTestGithubClient();
  // Wait for 5 seconds to make sure we could get the latest pull request list
  await new Promise((resolve) => setTimeout(resolve, 5000));
  const { data: sdkPRs } = await github.pulls.list({
    owner: repoOwner,
    repo: repoName,
    state: 'all'
  });
  const result = sdkPRs.map((pr) => ({
    number: pr.number,
    state: pr.state,
    title: pr.title,
    base: pr.base.ref,
    head: pr.head.ref
  }));
  result.sort((a, b) => a.number - b.number);
  return result;
};

export const getLoggerTextForAssert = async (
  _logger: { allLogs: string[] },
  specRepoName: string,
  specPRNumber: number,
  sdkRepoName: string
) => {
  let text = _logger.allLogs.join('\n');
  text = text.replace(new RegExp(fs.realpathSync('.'), 'g'), '.');
  text = text.replace(new RegExp(repoOwner, 'g'), '<org>');
  text = text.replace(new RegExp(specRepoName, 'g'), '<spec_repo>');
  text = text.replace(new RegExp(sdkRepoName, 'g'), '<sdk_repo>');

  text = text.replace(/ [a-z0-9]{40}/g, ` <commit_sha>`);

  const version = require('pkginfo').read(module).package.version;
  text = text.replace(new RegExp(version.replace('.', '\\.'), 'g'), '<version>');

  text = text.replace(new RegExp(path.resolve(workPrefix), 'g'), '<work_dir>');

  return text;
};

export const launchTestSDKAutomation = async (automationPromise: Promise<void>, info: string) => {
  console.log(`Launching SDK Automation for ${info}`);
  try {
    await automationPromise;
  } catch (e) {
    console.log(e);
    // Need log for diagnostic so do not fail on exception
  }
};
