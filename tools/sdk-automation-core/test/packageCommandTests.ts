import { getInMemoryLogger, InMemoryLogger, Logger } from '@azure/logger-js';
import {
  assertEx,
  BlobStorageAppendBlob,
  BlobStorageBlob,
  BlobStoragePrefix,
  FakeCompressor,
  FakeRunner,
  InMemoryBlobStorage,
  RealRunner,
  ExecutableGit
} from '@ts-common/azure-js-dev-tools';
import { assert } from 'chai';
import { csharp } from '../lib';
import { createPackageCommandOptions, PackageCommand, runPackageCommands } from '../lib/langSpecs/packageCommand';
import { PackageCommandOptions } from '../lib/packageCommandOptions';
import { SDKRepository } from '../lib/sdkRepository';
import {
  SDKRepositoryPackage,
  SDKRepositoryPackageContext,
  SDKRepositoryPackageData
} from '../lib/langSpecs/sdkRepositoryPackage';
import { createSDKRepository } from './repositoryCommandTests';

describe('packageCommand.ts', function() {
  it('createPackageCommandOptions()', function() {
    const sdkRepository: SDKRepository = createSDKRepository();
    const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage();

    const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
      sdkRepository,
      sdkRepositoryPackage
    );

    assertEx.defined(packageCommandOptions.captureError, 'packageCommandOptions.captureError');
    assertEx.defined(packageCommandOptions.captureOutput, 'packageCommandOptions.captureOutput');
    assert.strictEqual(packageCommandOptions.changedFilePaths, sdkRepositoryPackage.data.changedFilePaths);
    assertEx.defined(packageCommandOptions.compressor, 'packageCommandOptions.compressor');
    assert.strictEqual(packageCommandOptions.environmentVariables, undefined);
    assert.strictEqual(packageCommandOptions.executionFolderPath, sdkRepositoryPackage.getRootedPackageFolderPath());
    assertEx.defined(packageCommandOptions.log, 'packageCommandOptions.log');
    assert.strictEqual(packageCommandOptions.logger, sdkRepositoryPackage.logger);
    assert.strictEqual(packageCommandOptions.packageData, sdkRepositoryPackage.data);
    assert.strictEqual(packageCommandOptions.relativePackageFolderPath, sdkRepositoryPackage.data.relativeFolderPath);
    assert.strictEqual(packageCommandOptions.repositoryData, sdkRepository.data);
    assert.strictEqual(packageCommandOptions.repositoryFolderPath, sdkRepositoryPackage.repositoryFolderPath);
    assert.strictEqual(
      packageCommandOptions.rootedPackageFolderPath,
      sdkRepositoryPackage.getRootedPackageFolderPath()
    );
    assertEx.defined(packageCommandOptions.runner, 'packageCommandOptions.runner');
    assert.strictEqual(packageCommandOptions.showCommand, true);
    assert.strictEqual(packageCommandOptions.showEnvironmentVariables, undefined);
    assert.strictEqual(packageCommandOptions.showResult, true);
  });

  describe('runPackageCommands()', function() {
    it('with one string command that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, 'dir', packageCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [`j/u: dir`, `Exit Code: 0`]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'inProgress');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command that returns 1', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 1, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, 'dir', packageCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [
        `j/u: dir`,
        `Exit Code: 1`,
        `Failed to create the package t.`
      ]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'failed');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command that returns an error', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new RealRunner();
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, 'food', packageCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [
        `j/u: food`,
        `Error: spawn food ENOENT`,
        `Exit Code: undefined`,
        `Failed to create the package t.`,
        `  Error: spawn food ENOENT`
      ]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'failed');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command array that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, ['dir'], packageCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [`j/u: dir`, `Exit Code: 0`]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'inProgress');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one string command array that returns 1', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 1, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, ['dir'], packageCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [
        `j/u: dir`,
        `Exit Code: 1`,
        `Failed to create the package t.`
      ]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'failed');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with two string command array that returns 0', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const runner = new FakeRunner();
      runner.set({ executable: 'dir', result: { exitCode: 0, stdout: 'dir-stdout', stderr: 'dir-stderr' } });
      runner.set({ executable: 'foo', result: { exitCode: 0, stdout: 'foo-stdout', stderr: 'foo-stderr' } });
      const sdkRepository: SDKRepository = createSDKRepository(runner, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );

      const result: boolean = await runPackageCommands(sdkRepositoryPackage, ['dir', 'foo'], packageCommandOptions);

      assert.strictEqual(result, true);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [`j/u: dir`, `Exit Code: 0`, `j/u: foo`, `Exit Code: 0`]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'inProgress');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });

    it('with one function command that throws an error', async function() {
      const sdkRepositoryLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepository: SDKRepository = createSDKRepository(undefined, sdkRepositoryLogger);
      const sdkRepositoryPackageLogger: InMemoryLogger = getInMemoryLogger();
      const sdkRepositoryPackage: SDKRepositoryPackage = createSDKRepositoryPackage(sdkRepositoryPackageLogger);
      const packageCommandOptions: PackageCommandOptions = createPackageCommandOptions(
        sdkRepository,
        sdkRepositoryPackage
      );
      const command: PackageCommand = () => {
        throw new Error('hello');
      };
      const result: boolean = await runPackageCommands(sdkRepositoryPackage, command, packageCommandOptions);

      assert.strictEqual(result, false);
      assert.deepEqual(sdkRepositoryLogger.allLogs, []);
      assert.deepEqual(sdkRepositoryPackageLogger.allLogs, [`Error: hello`]);
      assert.strictEqual(sdkRepositoryPackage.data.status, 'failed');
      assert.strictEqual(sdkRepository.data.status, 'inProgress');
    });
  });
});

function createSDKRepositoryPackage(logger?: Logger): SDKRepositoryPackage {
  const blobStorage = new InMemoryBlobStorage();
  const sdkRepositoryFolderPath = 'j';
  const sdkRepositoryPackageLogsBlob: BlobStorageAppendBlob = blobStorage.getAppendBlob('k');
  const sdkRepositoryPackageSpecPRIterationPrefix: BlobStoragePrefix = blobStorage.getPrefix('l');
  const sdkRepositoryPackageSpecPRPrefix: BlobStoragePrefix = blobStorage.getPrefix('l2');
  const sdkRepositoryPackageContext: SDKRepositoryPackageContext = {
    createCompressor: () => new FakeCompressor(),
    getBlobProxyUrl: (blob: BlobStorageBlob) => blob.getURL(),
    git: new ExecutableGit(),
    writeGenerationData: () => Promise.resolve()
  };
  const sdkRepositoryPackageData: SDKRepositoryPackageData = {
    changedFilePaths: [],
    generationBranch: 'm',
    generationRepository: 'n',
    generationRepositoryUrl: 'o',
    integrationBranch: 'p',
    integrationRepository: 'q',
    useIntegrationBranch: true,
    logsBlobUrl: 'r',
    mainRepository: 's',
    name: 't',
    relativeFolderPath: 'u',
    status: 'inProgress'
  };
  return new SDKRepositoryPackage(
    sdkRepositoryFolderPath,
    csharp,
    sdkRepositoryPackageLogsBlob,
    sdkRepositoryPackageSpecPRIterationPrefix,
    sdkRepositoryPackageSpecPRPrefix,
    logger || getInMemoryLogger(),
    sdkRepositoryPackageContext,
    sdkRepositoryPackageData
  );
}
