import fs from 'fs';
import path from 'path';
import { ensureDir, remove, writeFile } from 'fs-extra';
import { afterAll, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { ModularSDKType, RunMode, type ModularClientPackageOptions, type PackageResult } from '../../common/types.js';

const mocks = vi.hoisted(() => ({
  events: [] as string[],
  packageDirectory: '',
  modularSDKType: 'ManagementPlane',
  commandCalls: [] as Array<{ command: string; args: readonly string[]; errorAsWarning: boolean | undefined }>,
  changedPackageDirectories: new Set<string>(),
}));

vi.mock('child_process', () => ({
  execSync: vi.fn((command: string) => {
    mocks.events.push(`exec:${command}`);
  }),
}));

vi.mock('glob', () => ({
  glob: vi.fn(async () => [`${mocks.packageDirectory}/temp/sample.api.json`]),
}));

vi.mock('../../common/devToolUtils.js', () => ({
  customizePackage: vi.fn(async (packageDirectory: string) => {
    mocks.events.push(`customize:${packageDirectory}`);
  }),
  lintFix: vi.fn(async (packageDirectory: string) => {
    mocks.events.push(`lint:${packageDirectory}`);
  }),
  formatSdk: vi.fn(async (packageDirectory: string) => {
    mocks.events.push(`format:${packageDirectory}`);
  }),
  updateSnippets: vi.fn(async (packageDirectory: string) => {
    mocks.events.push(`snippets:${packageDirectory}`);
  }),
}));

vi.mock('../../common/utils.js', () => ({
  runCommandOptions: { shell: true },
  runCommand: vi.fn(
    async (
      command: string,
      args: readonly string[],
      _options: unknown,
      _realtimeOutput?: boolean,
      _timeoutSeconds?: number,
      errorAsWarning?: boolean
    ) => {
      mocks.events.push(`run:${command} ${args.join(' ')}`);
      mocks.commandCalls.push({ command, args, errorAsWarning });
      return { stdout: '', stderr: '', code: 0 };
    }
  ),
  cleanupSamplesFolder: vi.fn(async () => undefined),
  cleanUpPackageDirectory: vi.fn(async () => undefined),
  defaultChildProcessTimeout: 60_000,
  generateRepoDataInTspLocation: vi.fn(() => 'repo-data'),
  getGeneratedPackageDirectory: vi.fn(async () => mocks.packageDirectory),
  sanitizeAdditionalArgs: vi.fn((value: string) => value),
  specifyApiVersionToGenerateSDKByTypeSpec: vi.fn(),
}));

vi.mock('../../common/npmUtils.js', () => ({
  getNpmPackageInfo: vi.fn(async () => ({ name: '@azure/sample', version: '1.0.0' })),
  getArtifactName: vi.fn(() => 'sample.tgz'),
}));

vi.mock('../../utils/generateInputUtils.js', () => ({
  getModularSDKType: vi.fn(() => mocks.modularSDKType),
}));

vi.mock('../../utils/git.js', () => ({
  getChangedCiYmlFilesInSpecificFolder: vi.fn(async () => []),
  getChangedPackageDirectory: vi.fn(async () => mocks.changedPackageDirectories),
}));

vi.mock('../../utils/getOutputPackageInfo.js', () => ({
  getOutputPackageInfo: vi.fn(() => ({
    language: 'JavaScript',
    packageName: '',
    version: '',
    path: [],
    changelog: { content: '', hasBreakingChange: false, breakingChangeItems: [] },
    artifacts: [],
    apiViewArtifact: '',
    result: 'succeeded',
    packageFolder: '',
  })),
}));

vi.mock('../../common/changelog/automaticGenerateChangeLogAndBumpVersion.js', () => ({
  generateChangelogAndBumpVersion: vi.fn(async () => ({
    content: '',
    hasBreakingChange: false,
    breakingChangeItems: [],
    changelogItems: { breakingChanges: new Map() },
  })),
}));

vi.mock('../../common/ciYamlUtils.js', () => ({
  createOrUpdateCiYaml: vi.fn(async () => undefined),
}));

vi.mock('../../utils/changeCiYaml.js', () => ({
  modifyOrGenerateCiYml: vi.fn(async () => undefined),
}));

vi.mock('../../utils/changeConfigOfTestAndSample.js', () => ({
  ChangeModel: { Change: 'Change', Revert: 'Revert' },
  SdkType: { Hlc: 'Hlc', Rlc: 'Rlc' },
  changeConfigOfTestAndSample: vi.fn(),
}));

vi.mock('../../utils/addApiViewInfo.js', () => ({
  addApiViewInfo: vi.fn(),
}));

vi.mock('../../hlc/utils/changeReadmeMd.js', () => ({
  changeReadmeMd: vi.fn(),
}));

vi.mock('../../hlc/utils/getReleaseTool.js', () => ({
  getReleaseTool: vi.fn(() => 'test'),
}));

vi.mock('../../llc/utils/prepareCommandToInstallDependenciesForTypeSpecProject.js', () => ({
  prepareCommandToInstallDependenciesForTypeSpecProject: vi.fn(() => 'install TypeSpec dependencies'),
}));

vi.mock('../../llc/utils/generateSampleReadmeMd.js', () => ({
  replaceRequireInAutorestConfigurationFile: vi.fn(),
}));

vi.mock('../../llc/utils/updateTypeSpecProjectYamlFile.js', () => ({
  updateTypeSpecProjectYamlFile: vi.fn(),
}));

import { buildPackage } from '../../common/rushUtils.js';
import { generateMgmt } from '../../hlc/generateMgmt.js';
import { generateRLCInPipeline } from '../../llc/generateRLCInPipeline/generateRLCInPipeline.js';

const fixtureRoot = path.join(process.cwd(), 'src', 'test', 'pipeline', 'tmp', 'customization-lifecycle');
const packageDirectory = path.join(fixtureRoot, 'sdk', 'service', 'sample');
const relativePackageDirectory = path.relative(fixtureRoot, packageDirectory);

function packageResult(): PackageResult {
  return {
    language: 'JavaScript',
    packageName: '',
    version: '',
    path: [],
    changelog: { content: '', hasBreakingChange: false, breakingChangeItems: [] },
    artifacts: [],
    apiViewArtifact: '',
    result: 'succeeded',
    packageFolder: '',
  };
}

function expectLifecycleOrder(buildEvent: string) {
  const installIndex = mocks.events.indexOf('exec:pnpm install');
  const runInstallIndex = mocks.events.indexOf('run:pnpm install');
  const effectiveInstallIndex = Math.max(installIndex, runInstallIndex);
  const customizeEvents = mocks.events.filter((event) => event === `customize:${packageDirectory}`);
  const customizeIndex = mocks.events.indexOf(`customize:${packageDirectory}`);
  const lintIndex = mocks.events.indexOf(`lint:${packageDirectory}`);
  const buildIndex = mocks.events.indexOf(buildEvent);

  expect(customizeEvents).toHaveLength(1);
  expect(effectiveInstallIndex).toBeGreaterThanOrEqual(0);
  expect(customizeIndex).toBeGreaterThan(effectiveInstallIndex);
  expect(lintIndex).toBeGreaterThan(customizeIndex);
  expect(buildIndex).toBeGreaterThan(customizeIndex);
}

describe('generation customization lifecycle', () => {
  beforeAll(async () => {
    await ensureDir(path.join(packageDirectory, 'temp'));
    await writeFile(
      path.join(packageDirectory, 'package.json'),
      JSON.stringify({ name: '@azure/sample', version: '1.0.0' }),
      'utf8'
    );
    await writeFile(path.join(packageDirectory, 'temp', 'sample.api.json'), '{}', 'utf8');
  });

  beforeEach(() => {
    mocks.events.length = 0;
    mocks.commandCalls.length = 0;
    mocks.packageDirectory = packageDirectory;
    mocks.changedPackageDirectories = new Set([relativePackageDirectory]);
    mocks.modularSDKType = ModularSDKType.ManagementPlane;
  });

  afterAll(async () => {
    await remove(fixtureRoot);
  });

  test.each([
    [ModularSDKType.ManagementPlane, false],
    [ModularSDKType.DataPlane, true],
  ])('customizes each %s modular package before lint and build', async (sdkType, buildErrorAsWarning) => {
    mocks.modularSDKType = sdkType;
    const options: ModularClientPackageOptions = {
      sdkRepoRoot: fixtureRoot,
      specRepoRoot: fixtureRoot,
      typeSpecDirectory: fixtureRoot,
      gitCommitId: 'commit',
      skip: false,
      repoUrl: 'https://example.invalid/spec.git',
      versionPolicyName: 'client',
      local: true,
      apiVersion: undefined,
      sdkReleaseType: undefined,
      runMode: RunMode.Release,
    };

    await buildPackage(packageDirectory, options, packageResult());

    expectLifecycleOrder('run:pnpm turbo build --filter @azure/sample... --token 1');
    const buildCall = mocks.commandCalls.find(
      ({ command, args }) => command === 'pnpm' && args[0] === 'turbo' && args[1] === 'build'
    );
    expect(buildCall?.errorAsWarning).toBe(buildErrorAsWarning);
  });

  test('customizes an RLC package once after install and before lint and build', async () => {
    await generateRLCInPipeline({
      sdkRepo: fixtureRoot,
      swaggerRepo: fixtureRoot,
      readmeMd: undefined,
      typespecProject: 'specification/sample',
      sdkGenerationType: 'script',
      swaggerRepoUrl: 'https://example.invalid/spec',
      gitCommitId: 'commit',
      typespecEmitter: '@azure-tools/typespec-ts',
      skipGeneration: false,
      apiVersion: undefined,
      sdkReleaseType: undefined,
      runMode: RunMode.Release,
    });

    expectLifecycleOrder('exec:pnpm build --filter @azure/sample...');
  });

  test('customizes an HLC package once after install and before lint and build', async () => {
    expect(fs.existsSync(path.join(packageDirectory, 'package.json'))).toBe(true);

    await generateMgmt({
      sdkRepo: fixtureRoot,
      swaggerRepo: fixtureRoot,
      readmeMd: 'specification/sample/readme.md',
      gitCommitId: 'commit',
      skipGeneration: true,
      apiVersion: undefined,
      sdkReleaseType: undefined,
      runMode: RunMode.Release,
    });

    expectLifecycleOrder('exec:pnpm build --filter @azure/sample...');
  });
});
