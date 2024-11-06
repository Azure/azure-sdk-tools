import { getInMemoryLogger, InMemoryLogger, Logger } from '@azure/logger-js';
import {
  assertEx,
  BlobStorageAppendBlob,
  BlobStorageBlob,
  BlobStoragePrefix,
  FakeCompressor,
  FakeGitHub,
  FakeHttpClient,
  FakeRunner,
  InMemoryBlobStorage,
  RealRunner,
  Runner,
  ExecutableGit
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { dotnet } from '../lib/langSpecs/dotnet';
import { LanguageConfiguration } from '../lib/langSpecs/languageConfiguration';
import {
  createRepositoryCommandOptions,
  RepositoryCommand,
  runRepositoryCommands
} from '../lib/langSpecs/repositoryCommand';
import { RepositoryCommandOptions } from '../lib/repositoryCommandOptions';
import { getBlobLogger } from '../lib/sdkAutomation';
import { SDKRepository, SDKRepositoryContext, SDKRepositoryData } from '../lib/sdkRepository';
import { SwaggerToSDKConfiguration } from '../lib/swaggerToSDKConfiguration';

describe('repositoryCommand.ts', function() {
  it('createRepositoryCommandOptions()', function() {
    const sdkRepository: SDKRepository = createSDKRepository();
    const repositoryFolderPath = 'a';

    const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
      sdkRepository,
      repositoryFolderPath
    );

    assertEx.defined(repositoryCommandOptions.captureError, 'repositoryCommandOptions.captureError');
    assertEx.defined(repositoryCommandOptions.captureOutput, 'repositoryCommandOptions.captureOutput');
    assertEx.defined(repositoryCommandOptions.compressor, 'repositoryCommandOptions.compressor');
    assert.strictEqual(repositoryCommandOptions.environmentVariables, undefined);
    assert.strictEqual(repositoryCommandOptions.executionFolderPath, repositoryFolderPath);
    assertEx.defined(repositoryCommandOptions.log, 'repositoryCommandOptions.log');
    assert.strictEqual(repositoryCommandOptions.logger, sdkRepository.logger);
    assert.strictEqual(repositoryCommandOptions.repositoryData, sdkRepository.data);
    assert.strictEqual(repositoryCommandOptions.repositoryFolderPath, repositoryFolderPath);
    assertEx.defined(repositoryCommandOptions.runner, 'repositoryCommandOptions.runner');
    assert.strictEqual(repositoryCommandOptions.showCommand, true);
    assert.strictEqual(repositoryCommandOptions.showEnvironmentVariables, undefined);
    assert.strictEqual(repositoryCommandOptions.showResult, true);
  });

  describe('runRepositoryCommands()', function() {
    it('with one string command that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, 'dir', repositoryCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [`${repositoryFolderPath}: dir`, `Exit Code: 0`]);
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command that returns 1', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 1, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, 'dir', repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [
        `${repositoryFolderPath}: dir`,
        `Exit Code: 1`,
        `Failed to run the command for ${sdkRepository.data.mainRepository}.`
      ]);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });

    it('with one string command that returns an error', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new RealRunner();
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, 'food', repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [
        `${repositoryFolderPath}: food`,
        `Error: spawn food ENOENT`,
        `Exit Code: undefined`,
        `Failed to run the command for ${sdkRepository.data.mainRepository}.`,
        `  Error: spawn food ENOENT`
      ]);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });

    it('with one string command array that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, ['dir'], repositoryCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [`${repositoryFolderPath}: dir`, `Exit Code: 0`]);
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command array that returns 1', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 1, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, ['dir'], repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [
        `${repositoryFolderPath}: dir`,
        `Exit Code: 1`,
        `Failed to run the command for ${sdkRepository.data.mainRepository}.`
      ]);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });

    it('with one string command array that returns an error', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new RealRunner();
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, ['food'], repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [
        `${repositoryFolderPath}: food`,
        `Error: spawn food ENOENT`,
        `Exit Code: undefined`,
        `Failed to run the command for ${sdkRepository.data.mainRepository}.`,
        `  Error: spawn food ENOENT`
      ]);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });

    it('with two string command array that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      runner.set({ executable: 'foo', result: { exitCode: 0, stdout: 'foo-stdout', stderr: 'foo-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );

      const result: boolean = await runRepositoryCommands(sdkRepository, ['dir', 'foo'], repositoryCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [
        `${repositoryFolderPath}: dir`,
        `Exit Code: 0`,
        `${repositoryFolderPath}: foo`,
        `Exit Code: 0`
      ]);
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one function command that throws an error', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepository: SDKRepository = createSDKRepository(undefined, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );
      const command: RepositoryCommand = () => {
        throw new Error('hello');
      };

      const result: boolean = await runRepositoryCommands(sdkRepository, command, repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, [`Error: hello`]);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });

    it('with one function marks the repository as failed', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepository: SDKRepository = createSDKRepository(undefined, sdkRepositoryLogger);
      const repositoryFolderPath = 'a';
      const repositoryCommandOptions: RepositoryCommandOptions = createRepositoryCommandOptions(
        sdkRepository,
        repositoryFolderPath
      );
      const command: RepositoryCommand = (options: RepositoryCommandOptions) => {
        options.repositoryData.status = 'failed';
      };

      const result: boolean = await runRepositoryCommands(sdkRepository, command, repositoryCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.strictEqual(sdkRepository.data.status, 'failed');
    });
  });
});

export function createSDKRepository(runner?: Runner, logger?: Logger): SDKRepository {
  const blobStorage = new InMemoryBlobStorage();
  const sdkRepositoryLogsBlob: BlobStorageAppendBlob = blobStorage.getAppendBlob('a');
  const language: LanguageConfiguration = dotnet;
  const swaggerToSDKConfiguration: SwaggerToSDKConfiguration = {};
  const sdkRepositorySpecPRIterationPrefix: BlobStoragePrefix = blobStorage.getPrefix('b');
  const sdkRepositorySpecPRPrefix: BlobStoragePrefix = blobStorage.getPrefix('b');
  const sdkRepositoryContext: SDKRepositoryContext = {
    createCompressor: () => new FakeCompressor(),
    deleteClonedRepositories: false,
    getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL(),
    github: new FakeGitHub(),
    git: new ExecutableGit(),
    httpClient: new FakeHttpClient(),
    runner: runner || new FakeRunner(),
    writeGenerationData: () => Promise.resolve(),
    getBlobLogger,
    createGenerationPullRequests: true,
    specificationPullRequest: {
      baseRepository: 'spec-pr-base-repository',
      headCommit: 'spec-pr-head-commit',
      headRepository: 'spec-pr-head-repository',
      htmlUrl: 'spec-pr-html-url',
      number: 7,
      title: 'spec-pr-title'
    }
  };
  const sdkRepositoryData: SDKRepositoryData = {
    generationRepository: 'c',
    generationRepositoryUrl: 'd',
    integrationRepository: 'e',
    integrationRepositoryUrl: 'f',
    mainRepository: 'g',
    mainRepositoryUrl: 'h',
    integrationBranchPrefix: 'sdkAutomationTest',
    mainBranch: 'master',
    languageName: language.name,
    readmeMdFileUrlsToGenerate: [],
    status: 'inProgress',
    swaggerToSDKConfigFileUrl: 'i'
  };
  return new SDKRepository(
    sdkRepositoryLogsBlob,
    logger || getInMemoryLogger(),
    dotnet,
    swaggerToSDKConfiguration,
    sdkRepositorySpecPRIterationPrefix,
    sdkRepositorySpecPRPrefix,
    sdkRepositoryContext,
    sdkRepositoryData
  );
}
