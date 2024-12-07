import convict, { Config } from 'convict';
import * as dotenv from 'dotenv';
import { homedir } from 'os';
import path from 'path';

dotenv.config();

export type SDKAutomationCliConfig = {
  env: string;
  workingFolder: string;
  isTriggeredByPipeline: boolean;
  githubToken: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;
  sdkRepoName: string;
  prNumber: number;
  specCommitSha: string;
  specRepoHttpsUrl: string;
  githubCommentAuthorName: string;
};

const emptyValidator = (value?: string): void => {
  if (value === undefined || value === '') {
    throw new Error(`ConfigError: spec repo cannot be empty or undefined. Please set the 'SPEC_REPO' environment variable or pass in the value when run the tool.`);
  }
};

export const configurationSchema: Config<SDKAutomationCliConfig> = convict<SDKAutomationCliConfig>({
  env: {
    default: '',
    env: 'ENV',
    arg: 'env',
    format: String
  },
  workingFolder: {
    default: path.join(homedir(), '.sdkauto'),
    env: 'WORKING_FOLDER',
    arg: 'working-folder',
    format: String
  },
  isTriggeredByPipeline: {
    default: false,
    env: 'IS_TRIGGERED_BY_Pipeline',
    arg: 'is-triggered-by-pipeline',
    format: Boolean
  },
  githubToken: {
    default: '',
    doc: 'Generate from https://github.com/settings/tokens/new. Keep it empty if want to use github app',
    env: 'GITHUB_TOKEN',
    arg: 'github-token',
    format: String
  },
  localSpecRepoPath: {
    default: '',
    doc: 'Example: /path/to/azure-rest-api-specs',
    env: 'LOCAL_SPEC_REPO_PATH',
    arg: 'local-spec-repo-path',
    format: emptyValidator
  },
  localSdkRepoPath: {
    default: '',
    doc: 'Example: /path/to/azure-sdk-for-go',
    env: 'LOCAL_SDK_REPO_PATH',
    arg: 'local-sdk-repo-path',
    format: emptyValidator
  },
  tspConfigPath: {
    default: '',
    doc: 'Example: specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml',
    env: 'TSP_CONFIG_RELATIVE_PATH',
    arg: 'tsp-config-relative-path',
    format: String
  },
  readmePath: {
    default: '',
    doc: 'Example: specification/contosowidgetmanager/resource-manager/readme.md',
    env: 'README_RELATIVE_PATH',
    arg: 'readme-relative-path',
    format: String
  },
  sdkRepoName: {
    default: '',
    doc: 'Example: azure-sdk-for-go',
    env: 'SDK_REPO_NAME',
    arg: 'sdk-repo-name',
    format: String
  },
  prNumber: {
    default: 0,
    doc: 'Pull Request Number',
    env: 'PR_NUMBER',
    arg: 'pr',
    format: Number
  },
  specCommitSha: {
    default: '',
    doc: 'Commit sha of the spec pull request',
    env: 'SPEC_COMMIT_SHA',
    arg: 'spec-commit-sha',
    format: String
  },
  specRepoHttpsUrl: {
    default: '',
    doc: 'https://github.com/azure/azure-rest-api-specs',
    env: 'SPEC_REPO_HTTPS_URL',
    arg: 'spec-repo-https-url',
    format: String
  },
  githubCommentAuthorName: {
    default: 'openapi-bot[bot]',
    doc: 'Comment author name shown on github comment. Add [bot] if githubApp is used',
    env: 'GITHUB_COMMENT_AUTHOR_NAME',
    arg: 'comment-author-name',
    format: String
  }
});
