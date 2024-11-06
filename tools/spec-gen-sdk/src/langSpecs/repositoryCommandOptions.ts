import { Logger } from '@azure/logger-js';
import { Compressor, RunOptions } from '@ts-common/azure-js-dev-tools';
import { SDKRepositoryData } from '../sdkRepository';

/**
 * Options that can be used within a repository command.
 */
export interface RepositoryCommandOptions extends RunOptions {
  /**
   * The head commit id of the specification pull request.
   */
  readonly specificationPullRequestHeadCommitId?: string;
  /**
   * The path to the repository folder.
   */
  readonly repositoryFolderPath: string;
  /**
   * The Compressor that can be used to create archives.
   */
  readonly compressor: Compressor;
  /**
   * The logger that can be used to write logs.
   */
  readonly logger: Logger;
  /**
   * The data associated with the repository.
   */
  readonly repositoryData: SDKRepositoryData;
}
