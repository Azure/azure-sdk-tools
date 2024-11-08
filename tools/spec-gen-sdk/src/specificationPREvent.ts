import { Logger } from '@azure/logger-js';
import { BlobStoragePrefix, ExecutableGit, GitHub, GitHubPullRequestWebhookBody } from '@ts-common/azure-js-dev-tools';

/**
 * A data type that contains the GitHub pull request webhook body that GitHub sent when an OpenAPI
 * specification pull request is changed.
 */
export interface SpecificationPREvent {
  /**
   * The webhook body that was sent by GitHub when the OpenAPI specification pull request was
   * changed.
   */
  readonly webhookBody: GitHubPullRequestWebhookBody;
  /**
   * The blob storage prefix that should be used to store all data that is created as a result of
   * the automation.
   */
  readonly workingPrefix: BlobStoragePrefix;
  /**
   * The new Logger that SDK Automation should use to log.
   */
  readonly logger?: Logger;
  /**
   * The new GitHub client that SDK Automation should use to interact with GitHub.
   */
  readonly github?: GitHub;
  /**
   * The new Git client that SDK Automation should use to interact with Git.
   */
  readonly git?: ExecutableGit;
}
