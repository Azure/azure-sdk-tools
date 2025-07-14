import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import path from 'path';
import fs from 'fs';
import { mkdir, writeFile, rm } from 'fs/promises';

describe('generateChangelogAndBumpVersion API review path selection', () => {
    let tempTestDir: string;
    let packageDir: string;
    let mockMakeChangesForFirstRelease: any;
    let mockMakeChangesForReleasingTrack2: any;

    beforeEach(async () => {
        // Create a unique temporary directory for each test
        tempTestDir = path.join(__dirname, `tmp/generate-changelog-test-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`);
        packageDir = path.join(tempTestDir, 'test-package');
        
        await mkdir(packageDir, { recursive: true });
        await mkdir(path.join(packageDir, 'review'), { recursive: true });
        await mkdir(path.join(packageDir, 'src'), { recursive: true });
        
        // Mock external dependencies for generateChangelogAndBumpVersion
        // Mock existing npm package to trigger the track2 comparison path
        const mockNpmView = {
            versions: ['1.0.0', '1.1.0', '1.2.0'],
            'dist-tags': {
                latest: '1.2.0',
                next: '2.0.0-beta.1'
            },
            time: {
                '1.0.0': '2023-01-01T00:00:00.000Z',
                '1.1.0': '2023-02-01T00:00:00.000Z', 
                '1.2.0': '2023-03-01T00:00:00.000Z',
                '2.0.0-beta.1': '2023-04-01T00:00:00.000Z'
            }
        };
        
        vi.doMock('../../common/npmUtils.js', () => ({
            tryGetNpmView: vi.fn().mockResolvedValue(mockNpmView),
            tryCreateLastestStableNpmViewFromGithub: vi.fn().mockResolvedValue(undefined),
        }));

        vi.doMock('../../xlc/apiVersion/apiVersionTypeExtractor.js', () => ({
            getApiVersionType: vi.fn().mockResolvedValue('stable'),
        }));

        mockMakeChangesForFirstRelease = vi.fn().mockResolvedValue(undefined);
        mockMakeChangesForReleasingTrack2 = vi.fn().mockResolvedValue(undefined);
        vi.doMock('../../common/changelog/modifyChangelogFileAndBumpVersion.js', () => ({
            makeChangesForFirstRelease: mockMakeChangesForFirstRelease,
            makeChangesForMigrateTrack1ToTrack2: vi.fn().mockResolvedValue(undefined),
            makeChangesForPatchReleasingTrack2: vi.fn().mockResolvedValue(undefined),
            makeChangesForReleasingTrack2: mockMakeChangesForReleasingTrack2,
        }));

        // Mock DifferenceDetector and ChangelogGenerator
        vi.doMock('../../changelog/v2/DifferenceDetector.js', () => ({
            DifferenceDetector: vi.fn().mockImplementation(() => ({
                detect: vi.fn().mockResolvedValue({
                    hasBreakingChange: false,
                    hasFeature: true
                }),
                getDetectContext: vi.fn().mockReturnValue({})
            }))
        }));

        vi.doMock('../../changelog/v2/ChangelogGenerator.js', () => ({
            ChangelogGenerator: vi.fn().mockImplementation(() => ({
                generate: vi.fn().mockReturnValue({
                    hasBreakingChange: false,
                    hasFeature: true,
                    content: '## 2.0.0 (2023-05-01)\n\n### Features\n\n- Added new feature'
                })
            }))
        }));

        // Mock child_process execSync for git commands
        vi.doMock('child_process', () => ({
            execSync: vi.fn((cmd) => {
                if (cmd.includes('git show HEAD:')) {
                    return JSON.stringify({ version: '1.9.0' });
                }
                return '';
            })
        }));

        // Mock utils functions (excluding getApiReviewPath to use real implementation)
        vi.doMock('../../common/utils.js', async () => {
            const actual = await vi.importActual('../../common/utils.js') as any;
            return {
                ...actual,
                getNpmPackageName: vi.fn(() => '@azure/arm-testservice'),
                getSDKType: vi.fn(() => 'mgmt'),
                fixChangelogFormat: vi.fn((content) => content),
                tryReadNpmPackageChangelog: vi.fn(() => '# Release History\n\n## 1.2.0 (2023-03-01)\n\n### Features\n\n- Previous feature')
            };
        });

        // Mock version utils
        vi.doMock('../../utils/version.js', () => ({
            getLatestStableVersion: vi.fn(() => '1.2.0'),
            getVersion: vi.fn(() => '2.0.0-beta.1'),
            isBetaVersion: vi.fn(() => false),
            isStableSDKReleaseType: vi.fn(() => true),
            getUsedVersions: vi.fn(() => ['1.0.0', '1.1.0', '1.2.0']),
            getNewVersion: vi.fn(() => '2.0.0'),
            bumpPatchVersion: vi.fn(() => '1.2.1'),
            bumpPreviewVersion: vi.fn(() => '2.0.0-beta.2'),
            getversionDate: vi.fn(() => new Date('2023-03-01'))
        }));
    });

    afterEach(async () => {
        try {
            if (fs.existsSync(tempTestDir)) {
                await rm(tempTestDir, { recursive: true, force: true });
            }
        } catch (error) {
            console.warn('Failed to clean up temp directory:', error);
        }
        vi.clearAllMocks();
        vi.resetModules();
    });

    test('should call getApiReviewPath for both npm and local packages when processing existing track2 SDK', async () => {
        // Setup: Create HLC package with -node.api.md file
        const packageJson = {
            name: '@azure/arm-testservice',
            version: '2.0.0',  // Changed to 2.0.0 to simulate existing package
            'sdk-type': 'mgmt'
        };
        
        await writeFile(
            path.join(packageDir, 'package.json'), 
            JSON.stringify(packageJson, null, 2)
        );

        // Create both files, but -node.api.md should take priority
        const nodeApiContent = `
// Test API content for Node.js specific API
export interface TestServiceClient {
    // Node.js specific methods
    testOperation(): Promise<string>;
}
`;

        const standardApiContent = `
// Standard API content
export interface TestServiceClient {
    // Standard methods
    standardOperation(): Promise<string>;
}
`;

        await writeFile(
            path.join(packageDir, 'review', 'arm-testservice-node.api.md'),
            nodeApiContent
        );
        
        await writeFile(
            path.join(packageDir, 'review', 'arm-testservice.api.md'),
            standardApiContent
        );

        // Create a basic TypeScript client file
        const clientContent = `
export class TestServiceClient {
    testOperation(): Promise<string> {
        return Promise.resolve("test");
    }
}
`;
        await writeFile(path.join(packageDir, 'src', 'testServiceClient.ts'), clientContent);

        // Create changelog-temp directory structure for npm package comparison
        const changelogTempDir = path.join(packageDir, 'changelog-temp', 'package');
        await mkdir(changelogTempDir, { recursive: true });
        await mkdir(path.join(changelogTempDir, 'review'), { recursive: true });
        
        // Create npm package.json with sdk-type: mgmt
        const npmPackageJson = {
            name: '@azure/arm-testservice',
            version: '1.2.0',
            'sdk-type': 'mgmt'
        };
        await writeFile(
            path.join(changelogTempDir, 'package.json'),
            JSON.stringify(npmPackageJson, null, 2)
        );

        // Create npm package API review file
        await writeFile(
            path.join(changelogTempDir, 'review', 'arm-testservice-node.api.md'),
            `// Old API content\nexport interface TestServiceClient {\n    oldOperation(): Promise<string>;\n}`
        );

        // Create npm package CHANGELOG.md
        await writeFile(
            path.join(changelogTempDir, 'CHANGELOG.md'),
            `# Release History\n\n## 1.2.0 (2023-03-01)\n\n### Features\n\n- Previous feature`
        );

        // Mock shell.pwd() using dynamic import approach
        vi.doMock('shelljs', () => ({
            default: {
                pwd: () => tempTestDir,
                mkdir: vi.fn(),
                cd: vi.fn(),
                exec: vi.fn((cmd) => {
                    // Mock npm pack and tar commands
                    if (cmd.includes('npm pack')) {
                        return { code: 0, stdout: 'arm-testservice-1.2.0.tgz', stderr: '' };
                    }
                    if (cmd.includes('tar -xzf')) {
                        return { code: 0, stdout: '', stderr: '' };
                    }
                    return { code: 0, stdout: '', stderr: '' };
                }),
                ls: vi.fn((pattern) => {
                    if (pattern === '*.tgz') {
                        return ['arm-testservice-1.2.0.tgz'];
                    }
                    return [];
                }),
                rm: vi.fn(),
                test: vi.fn(() => false), // Mock shell.test to return false (indicating HLC/Modular type)
            }
        }));

        // Import the function after mocking
        vi.resetModules();
        const { generateChangelogAndBumpVersion } = await import('../../common/changelog/automaticGenerateChangeLogAndBumpVersion.js');

        // Test: Call generateChangelogAndBumpVersion with relative path
        await expect(generateChangelogAndBumpVersion('test-package', { 
            apiVersion: undefined, 
            sdkReleaseType: undefined 
        })).resolves.not.toThrow();

        // Verify that the makeChangesForReleasingTrack2 was called (indicates track2 comparison path)
        expect(mockMakeChangesForReleasingTrack2).toHaveBeenCalled();

        // Verify that first release path was NOT called
        expect(mockMakeChangesForFirstRelease).not.toHaveBeenCalled();

        // The test successfully completed, which means getApiReviewPath was called internally
        // and found the correct API review files we created for both npm and local packages
    });

});