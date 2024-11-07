import { Command, parseCommands, run, getPathName } from '@ts-common/azure-js-dev-tools';
import { RepositoryCommandOptions } from './repositoryCommandOptions';
import { trimNewLine, errorToLog } from '../sdkAutomation';
import { replaceCommandVariables, SDKRepository } from '../sdkRepository';

export type RepositoryCommand = ((options: RepositoryCommandOptions) => void | Promise<void>) | string;

export function createRepositoryCommandOptions(
  repository: SDKRepository,
  rootedRepositoryFolderPath: string
): RepositoryCommandOptions {
  return {
    specificationPullRequestHeadCommitId:
      repository.context.specificationPullRequest && repository.context.specificationPullRequest.headCommit,
    repositoryFolderPath: rootedRepositoryFolderPath,
    compressor: repository.context.createCompressor(),
    logger: repository.logger,
    executionFolderPath: rootedRepositoryFolderPath,
    runner: repository.context.runner,
    showCommand: true,
    showResult: true,
    captureOutput: (text: string) => repository.logger.logInfo(trimNewLine(text)),
    captureError: (text: string) => repository.logger.logError(trimNewLine(text)),
    capturePrefix: 'GenCmd',
    throwOnError: true,
    log: (text: string) => repository.logger.logInfo(trimNewLine(text)),
    repositoryData: repository.data
  };
}

export async function runRepositoryCommands(
  repository: SDKRepository,
  repositoryCommands: RepositoryCommand | RepositoryCommand[],
  repositoryCommandOptions: RepositoryCommandOptions
): Promise<boolean> {
  let result = true;

  const repositoryCommandsArray: RepositoryCommand[] = Array.isArray(repositoryCommands)
    ? repositoryCommands
    : [repositoryCommands];
  try {
    for (const repositoryCommand of repositoryCommandsArray) {
      if (typeof repositoryCommand === 'string') {
        const parsedRepositoryCommands: Command[] = parseCommands(repositoryCommand);
        for (const parsedRepositoryCommand of parsedRepositoryCommands) {
          replaceCommandVariables(parsedRepositoryCommand, repositoryCommandOptions, repository.logger);
          await run(parsedRepositoryCommand, undefined, {
            ...repositoryCommandOptions,
            capturePrefix: getPathName(parsedRepositoryCommand.executable)
          });
        }
      } else {
        await Promise.resolve(repositoryCommand(repositoryCommandOptions));
      }
    }
  } catch (error) {
    await repository.logger.logError(`Failed to run repository command`);
    await repository.logger.logError(errorToLog(error, false));
    repository.data.status = 'failed';
    result = false;
  }

  return result;
}
