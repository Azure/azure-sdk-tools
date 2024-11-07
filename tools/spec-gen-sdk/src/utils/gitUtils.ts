import { SdkAutoContext } from '../automation/entrypoint';
import { CleanOptions, ResetMode, SimpleGit } from 'simple-git';

export const gitSetRemoteWithAuth = async (
  context: Pick<SdkAutoContext, 'logger' | 'getGithubAccessToken'>,
  repo: SimpleGit,
  remoteName: string,
  remoteRepo: {
    owner: string;
    name: string;
  }
) => {
  context.logger.log('git', `Set remote ${remoteName} on ${remoteRepo.owner}/${remoteRepo.name} with auth`);
  const token = await context.getGithubAccessToken(remoteRepo.owner);
  const url = `https://x-access-token:${token}@github.com/${remoteRepo.owner}/${remoteRepo.name}`;

  await gitAddRemote(repo, remoteName, url);
};

export const gitRemoveAllBranches = async (context: SdkAutoContext, repo: SimpleGit) => {
  const branches = await repo.branchLocal();
  if (branches.all.length === 0) { return; }
  let headCommit: string | undefined = undefined;
  try {
    headCommit = await repo.revparse('HEAD');
  } catch (error) {
    // unknown revision or path not in the working tree. Pass
    context.logger.warn('Failed to get HEAD commit. Error: ' + error.message);
  }

  if (headCommit) {
    await repo.raw(['checkout', headCommit]);
  }

  const currentBranch = await getCurrentBranch(repo);
  for (const branchRef in branches.branches) {
    if (branchRef && branchRef !== currentBranch) {
      try {
        await repo.deleteLocalBranch(branchRef);
        context.logger.log('git', `Delete branch ${branchRef}`);
      } catch (e) {
        context.logger.warn(`Failed to delete ${branchRef}. Error: ${e.message}`);
      }
    }
  }
};

export const gitGetDiffFileList = async (diff: string, context?: SdkAutoContext, description?: string) => {
  const patches = gitDiffResultToStringArray(diff);

  if (context && description) { context.logger.info(`${patches.length} changed files ${description}:`); }
  const fileList: string[] = [];
  for (const patch of patches) {
    const filePath = patch.path;
    if (context) { context.logger.info(`${patch.mode}\t${filePath}`); }
    fileList.push(filePath);
  }
  return fileList;
};

export const gitGetCommitter = async (repo: SimpleGit): Promise<void> => {
  await repo.raw(['config', 'user.name', 'SDKAuto']);
  await repo.raw(['config', 'user.email', '<sdkautomation@microsoft.com>']);
};

export const gitAddAll = async (repo: SimpleGit) => {
  await repo.add('*');
  return await repo.raw(['write-tree']);
};

export const gitCheckoutBranch = async (context: SdkAutoContext, repo: SimpleGit, branchName: string, reset: boolean = true) => {
  let headCommit: string | undefined = undefined;
  try {
    headCommit = await repo.revparse('HEAD');
  } catch (error) {
    // unknown revision or path not in the working tree. Pass
    context.logger.warn('Failed to get HEAD commit. Error: ' + error.message);
  }
  if (headCommit && reset) {
    await repo.clean(CleanOptions.RECURSIVE + CleanOptions.FORCE);
    await repo.reset(ResetMode.HARD, [headCommit]);
    await repo.checkout(branchName, ['--force']);
  } else {
    await repo.checkout(branchName, ['--force']);
  }
};

export type ListTree = {
  mode: string;
  type: string;
  object: string;
  file: string;
}[];

export type DiffPatches = {
  mode: string;
  path: string;
}[];

export const enum TreeMode {
  FILE = '100644',
  executable = '100755',
  subdirectory = '040000',
  submodule = '160000',
  UNTRACKED = '120000'
}

export const enum TreeType {
  BLOB = 'blob',
  TREE = 'tree',
  COMMIT = 'commit'
}

export function gitTreeResultToStringArray(treeResult: string): ListTree {
  if (treeResult === '') {
    return [];
  }
  const lines = treeResult.trim().split('\n');
  const resultArray = lines.map((line) => {
    const [mode, type, object, file] = line.split(/\s+/);
    return {
      mode,
      type,
      object,
      file
    };
  });

  return resultArray;
}

export function gitDiffResultToStringArray(diffResult: string): DiffPatches {
  if (diffResult === '') {
    return [];
  }
  const lines = diffResult.trim().split('\n');
  const resultArray = lines.map((line: string) => {
    const [mode, path] = line.split(/\s+/);
    return { mode, path };
  });
  return resultArray;
}

export async function gitAddRemote(repo: SimpleGit, remoteName: string, remoteUrl: string): Promise<void> {
  const remotes = await repo.raw(['remote', '-v']);
  const lines = remotes.trim().split('\n');
  const resultArray = lines.map((line: string) => {
    const [name, url] = line.split(/\s+/);
    return { name, url };
  });
  const hasRemote = resultArray.some((item) => item.name === remoteName && item.url === remoteUrl);
  if (hasRemote) {
    await repo.remote(['set-url', remoteName, remoteUrl]);
  } else {
    await repo.addRemote(remoteName, remoteUrl);
  }
}

export async function getCurrentBranch(repo: SimpleGit): Promise<string> {
  const branches = await repo.branchLocal();
  const currentBranchInfo = Object.values(branches.branches).filter(item => item.current === true);
  return currentBranchInfo[0].name;
}
