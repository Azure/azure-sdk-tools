import { getTestGithubClient, repoOwner } from './utils';
import { sdkAutomationCliConfig } from '../src/cli/config';
import { Octokit, RestEndpointMethodTypes } from '@octokit/rest';

type ReposListForOrgResponse = RestEndpointMethodTypes['repos']['listForOrg']['response']['data']

const cleanGithubTestRepos = async () => {
  const github = await getTestGithubClient();
  const reposRsp: ReposListForOrgResponse = await github.paginate(github.repos.listForOrg.endpoint.merge({
    org: repoOwner
  }));

  const runId = sdkAutomationCliConfig.testRunId;
  const prefixToMatch = runId ? `test-${runId}` : 'test';

  let repos = reposRsp.map(repo => repo.name);
  console.log(`Repos in ${repoOwner}:`);
  console.log(repos.join('\n'));

  console.log(`\nFilter: ${prefixToMatch}`);
  repos = repos.filter(name => name.startsWith(prefixToMatch));
  console.log(`Repos after filter:`);
  console.log(repos.join('\n'));

  const parallelCount = 4;
  const promises: Promise<void>[] = [];
  for (let i = 0; i < parallelCount; ++i) {
    promises.push(cleanReposWorker(github, repos));
  }
  await Promise.all(promises);
};

const cleanReposWorker = async (github: Octokit, repos: string[]) => {
  while (repos.length > 0) {
    const repoName = repos.shift() as string;
    console.log(`Cleaning up ${repoOwner}/${repoName}`);
    try {
      await github.repos.delete({ owner: repoOwner, repo: repoName });
    } catch (e) {
      console.log(`Failed to delete ${repoOwner}/${repoName}: ${e.message} ${e.stack}`);
    }
  }
};

// tslint:disable-next-line: no-floating-promises
cleanGithubTestRepos();
