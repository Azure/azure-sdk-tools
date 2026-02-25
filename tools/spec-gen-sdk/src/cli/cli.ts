import yargs from "yargs";
import { hideBin } from "yargs/helpers";
import { getRepository } from '../utils/repo';
import { SDKAutomationState } from '../automation/sdkAutomationState';
import { sdkAutoMain } from '../automation/entrypoint';
import { requireJsonc } from "../utils/requireJsonc";
import path from 'path';
import { homedir } from "os";

export const usageText = `
Usage: spec-gen-sdk [OPTIONS]

This tool will generate the SDK code using the local specification repository and local SDK repository. Ensure they have been cloned locally and the corresponding paths have been passed in.`;

export type SpecGenSdkCliConfig = {
  workingFolder: string;
  localSpecRepoPath: string;
  localSdkRepoPath: string;
  tspConfigPath?: string;
  readmePath?: string;
  sdkRepoName: string;
  apiVersion?: string;
  prNumber?: string;
  runMode: string;
  sdkReleaseType: string;
  specCommitSha: string;
  specRepoHttpsUrl: string;
  headRepoHttpsUrl?: string;
  headBranch?: string;
  version: string;
  skipSdkGenFromOpenapi?: string;
};

const initCliConfig = (argv) : SpecGenSdkCliConfig => {
  return {
    workingFolder: argv.workingFolder,
    localSpecRepoPath: argv.localSpecRepoPath,
    localSdkRepoPath: argv.localSdkRepoPath,
    tspConfigPath: argv.tspConfigRelativePath,
    readmePath: argv.readmeRelativePath,
    sdkRepoName: argv.sdkRepoName,
    apiVersion: argv.apiVersion,
    prNumber: argv.prNumber,
    runMode: argv.runMode,
    sdkReleaseType: argv.sdkReleaseType,
    specCommitSha: argv.specCommitSha,
    specRepoHttpsUrl: argv.specRepoHttpsUrl,
    headRepoHttpsUrl: argv.headRepoHttpsUrl,
    headBranch: argv.headBranch,
    version: packageJson.version,
    skipSdkGenFromOpenapi: argv.skipSdkGenFromOpenapi
  };
};

// tslint:disable-next-line: no-floating-promises
const generateSdk = async (config: SpecGenSdkCliConfig) => {
  const start = process.hrtime();

  let status: SDKAutomationState | undefined = undefined;
  try {
    const repo = getRepository(config.specRepoHttpsUrl);

    process.chdir(config.workingFolder);
    status = await sdkAutoMain({
      specRepo: repo,
      localSpecRepoPath: config.localSpecRepoPath,
      localSdkRepoPath: config.localSdkRepoPath,
      tspConfigPath: config.tspConfigPath,
      readmePath: config.readmePath,
      specCommitSha: config.specCommitSha,
      specRepoHttpsUrl: config.specRepoHttpsUrl,
      pullNumber: config.prNumber,
      sdkName: config.sdkRepoName.toLowerCase().replace('-pr', ''),
      apiVersion: config.apiVersion,
      runMode: config.runMode,
      sdkReleaseType: config.sdkReleaseType,
      workingFolder: config.workingFolder,
      headRepoHttpsUrl: config.headRepoHttpsUrl,
      headBranch: config.headBranch,
      runEnv: config.runMode !== "local" ? 'azureDevOps' : 'local',
      branchPrefix: 'sdkAuto',
      version: config.version,
      skipSdkGenFromOpenapi: config.skipSdkGenFromOpenapi
    });
  } catch (e) {
    console.error(e.message);
    console.error(e.stack);
    status = 'failed';
  }

  const elapsed = process.hrtime(start);
  console.log(`spec-gen-sdk execution time: ${elapsed[0]}s`);
  console.log(`spec-gen-sdk exit with status ${status}`);
  if (status !== undefined && ['warning', 'succeeded', 'notEnabled'].includes(status)) {
    process.exit(0);
  } else {
    process.exit(-1);
  }
};

const packageJson = requireJsonc(path.resolve(__dirname, '../../package.json'));

// eslint-disable-next-line @typescript-eslint/no-unused-expressions
yargs(hideBin(process.argv))
  .version(packageJson.version)
  .alias("v", "version")
  .scriptName("spec-gen-sdk")
  .command(
    '$0',
    'Generate SDK',
    (yargs) => {
      yargs.options({
        'local-spec-repo-path': {
          alias: "scp",
          type: "string",
          description: "Path to local specification repository. Example: /path/to/azure-rest-api-specs",
          demandOption: true,
        },
        'local-sdk-repo-path': {
          alias: "sdp",
          type: "string",
          description: "Path to local sdk repository. Example: /path/to/azure-sdk-for-go",
          demandOption: true,
        },
        'working-folder': {
          alias: "wf",
          type: "string",
          description: "The working folder to run this tool",
          default: path.join(homedir(), '.sdkauto')
        },
        'tsp-config-relative-path': {
          alias: "tcrp",
          type: "string",
          description: "Path to the tsp config file from the root folder of specification repository. Example: specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml",
        },
        'readme-relative-path': {
          alias: "rrp",
          type: "string",
          description: "Path to the readme file from the root folder of specification repository. Example: specification/contosowidgetmanager/resource-manager/readme.md",
        },
        'sdk-repo-name': {
          alias: "l",
          type: "string",
          description: "Name of the azure-sdk-for-<language> repository",
          demandOption: true,
        },
        'pr-number': {
          alias: "n",
          type: "string",
          description: "The spec pull request number",
        },
        'spec-commit-sha': {
          alias: "c",
          type: "string",
          description: "The spec commit sha to use for the generation",
          demandOption: true,
        },
        'spec-repo-https-url': {
          alias: "u",
          type: "string",
          description: "Url of the specification repository",
          default: "https://github.com/azure/azure-rest-api-specs"
        },
        'head-repo-https-url': {
          alias: "hu",
          type: "string",
          description: "Url of the head repository of the specification pull request",
        },
        'head-branch': {
          alias: "hb",
          type: "string",
          description: "The branch of the head repository of the specification pull request",
        },
        'api-version': {
          alias: "apiv",
          type: "string",
          description: "The version of the API spec to be used to generate the SDK",
        },
        'sdk-release-type': {
          alias: "srt",
          type: "string",
          description: "The release type of SDK, either 'beta' or 'stable'",
          default: "beta",
          choices: ['beta', 'stable']
        },
        'run-mode': {
          alias: "rm",
          type: "string",
          description: "The run mode of the tool, allowed values are 'local','spec-pull-request','release','batch'",
          default: "local",
          choices: ['local', 'spec-pull-request', 'release', 'batch'],
        },
        'skip-sdk-gen-from-openapi': {
          alias: "skg",
          type: "string",
          description: "skip sdk generation from openapi specs, either 'true' or 'false'"
        }
    })},
    async (argv) => {
      const config = initCliConfig(argv);
      await generateSdk(config);
    }
  )
  .usage(usageText)
  .help()
  .argv;
