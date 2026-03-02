import { describe, expect, test, vi } from 'vitest';
import { getApiVersionType } from '../../mlc/apiVersion/apiVersionTypeExtractor.js';
import { getApiVersionType as getApiVersionTypeInRLC } from '../../llc/apiVersion/apiVersionTypeExtractor.js';
import { join } from 'path';
import { ApiVersionType } from '../../common/types.js';
import { getApiVersionTypeFromNpm, tryFindApiVersionInRestClient } from '../../xlc/apiVersion/utils.js';
import { generateTestNpmView } from '../utils/utils.js';

describe('Modular client api-version Extractor', () => {
    test('new createClient function', async () => {
        const clientPath = join(__dirname, 'testCases/new/src/rest/newClient.ts');
        const version = tryFindApiVersionInRestClient(clientPath);
        expect(version).toBe('2024-03-01-preview');
    });

    test('get api version type from new createClient function', async () => {
        const root = join(__dirname, 'testCases/new/');
        const version = await getApiVersionType(root);
        expect(version).toBe(ApiVersionType.Preview);
    });

    test('old createClient function 1', async () => {
        const clientPath = join(__dirname, 'testCases/old1/src/rest/oldClient.ts');
        const version = tryFindApiVersionInRestClient(clientPath);
        expect(version).toBe('v1.1-preview.1');
    });

    test('get api version type from old createClient function 1', async () => {
        const root = join(__dirname, 'testCases/old1/');
        const version = await getApiVersionType(root);
        expect(version).toBe(ApiVersionType.Preview);
    });

    test('old createClient function 2', async () => {
        const clientPath = join(__dirname, 'testCases/old2/src/rest/oldClient.ts');
        const version = tryFindApiVersionInRestClient(clientPath);
        expect(version).toBe('2024-03-01');
    });

    test('get api version type from old createClient function 2', async () => {
        const root = join(__dirname, 'testCases/old2/');
        const version = await getApiVersionType(root);
        expect(version).toBe(ApiVersionType.Stable);
    });
});

describe('Rest client file fallbacks', () => {
    describe('Modular client', () => {
        test('src/api/xxxContext.ts exists', async () => {
            const root = join(__dirname, 'testCases/new-context/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("src/api/xxxContext.ts doesn't exists, fallback to src/rest/xxxClient.ts", async () => {
            const root = join(__dirname, 'testCases/new/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("metadata.json exists with stable apiVersion", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-json/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Stable);
        });
        test("metadata.json exists with preview apiVersion", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-json-preview/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("metadata.json exists with apiVersions - all stable", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-apiVersions-all-stable/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Stable);
        });
        test("metadata.json exists with apiVersions - with preview", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-apiVersions-with-preview/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("metadata.json exists with apiVersions - preview listed first", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-apiVersions-preview-first/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("metadata.json exists with apiVersions - single stable version", async () => {
            const root = join(__dirname, 'testCases/mlc-metadata-apiVersions-single/');
            const version = await getApiVersionType(root);
            expect(version).toBe(ApiVersionType.Stable);
        });
        test("Model only spec", async () => {
            const mockNpmUtils = await import("../../common/npmUtils.js");
            let npmViewCount = 0;
            
            const npmViewSpy = vi
                .spyOn(mockNpmUtils, "tryGetNpmView")
                .mockImplementation(async () => {
                    npmViewCount++;
                    // First getApiVersionType call:
                    if (npmViewCount === 1) 
                        return { "dist-tags": { latest: "1.0.0" } }; // For isModelOnly check
                    if (npmViewCount === 2)
                        return { "dist-tags": { latest: "1.0.0-beta.1" } }; // For getApiVersionTypeFromNpm - should return Preview
                    // Second getApiVersionType call:
                    if (npmViewCount === 3)
                        return { "dist-tags": { latest: "1.0.0" } }; // For isModelOnly check  
                    if (npmViewCount === 4)
                        return { "dist-tags": { latest: "1.0.0" } }; // For getApiVersionTypeFromNpm - should return Stable
                    return { "dist-tags": { latest: "1.0.0" } };
                });

            const githubCheckSpy = vi
                .spyOn(mockNpmUtils, "checkDirectoryExistsInGithub")
                .mockImplementation(async () => false); // Always return false to indicate model-only package

            const root = join(__dirname, "testCases/mlc-model-only/");
            const version1 = await getApiVersionType(root);
            expect(version1).toBe(ApiVersionType.Preview); // Beta version from npm
            const version2 = await getApiVersionType(root);
            expect(version2).toBe(ApiVersionType.Stable); // Stable version from npm
            
            npmViewSpy.mockRestore();
            githubCheckSpy.mockRestore();
        });
    });    
    describe('RLC', () => {
        test('src/xxxContext.ts exists', async () => {
            const root = join(__dirname, 'testCases/rlc-context/');
            const version = await getApiVersionTypeInRLC(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("src/xxxContext.ts doesn't exists, fallback to src/xxxClient.ts", async () => {
            const root = join(__dirname, 'testCases/rlc-client/');
            const version = await getApiVersionTypeInRLC(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("get source folder from readme", async () => {
            const root = join(__dirname, 'testCases/rlc-source-from-readme/');
            const version = await getApiVersionTypeInRLC(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("get source folder from src", async () => {
            const root = join(__dirname, 'testCases/rlc-source-from-src/');
            const version = await getApiVersionTypeInRLC(root);
            expect(version).toBe(ApiVersionType.Preview);
        });
        test("get api version in parameters.ts that has `api-version: string`", async () => {
            const root = join(__dirname, 'testCases/rlc-source-from-src-streaming/');
            const version = await getApiVersionTypeInRLC(root);
            expect(version).toBe(ApiVersionType.Stable);
        });
        test("Model only spec", async () => {
            const mockNpmUtils = await import("../../common/npmUtils.js");
            let count = 0;
            const spy = vi
                .spyOn(mockNpmUtils, "tryGetNpmView")
                .mockImplementation(async () => {
                    count++;
                    if (count === 1)
                        return { "dist-tags": { latest: "1.0.0-beta.1" } };
                    return { "dist-tags": { latest: "1.0.0" } };
                });

            const root = join(__dirname, "testCases/rlc-model-only/");
            const version1 = await getApiVersionTypeInRLC(root);
            expect(version1).toBe(ApiVersionType.Preview);
            const version2 = await getApiVersionTypeInRLC(root);
            expect(version2).toBe(ApiVersionType.Stable);
            spy.mockRestore();
        });
    });
});

describe('Get ApiVersion Type From Npm', () => {
    interface TestCase {
        latestVersion?: string;
        latestVersionDate?: string;
        betaVersion?: string;
        betaVersionDate?: string;
        expectedVersionType: ApiVersionType;
    }
    const cases: TestCase[] = [
        // stable version is latest
        {
            latestVersion: "1.0.0",
            latestVersionDate: "2025-06-20T09:13:48.079Z",
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-01T07:07:56.529Z",
            expectedVersionType: ApiVersionType.Stable
        },
        // beta version is latest
        {
            latestVersion: "1.0.0",
            latestVersionDate: "2025-06-01T09:13:48.079Z",
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-21T07:07:56.529Z",
            expectedVersionType: ApiVersionType.Stable
        },
        // only has latest tag in beta version (back compatibility)
        {
            latestVersion: "1.0.0-beta.1",
            betaVersion: undefined,
            expectedVersionType: ApiVersionType.Preview
        },
        // only has beta tag in beta version
        {
            latestVersion: undefined ,
            betaVersion: "1.0.0-beta.1",
            expectedVersionType: ApiVersionType.Preview
        },
        // only has latest tag in stable version
        {
            latestVersion: "1.0.0",
            betaVersion: undefined,
            expectedVersionType: ApiVersionType.Stable
        },
        // no stable or beta version, indicate no npm package, fallback to preview
        {
            latestVersion: undefined,
            betaVersion: undefined,
            expectedVersionType: ApiVersionType.Preview
        }
    ]
    test.each(cases)('Stable: $latestVersion on data: $latestVersionDate, Beta: $betaVersion on data $betaVersionDate, Expected:$expectedVersionType',
        async ({latestVersion, betaVersion, expectedVersionType, latestVersionDate, betaVersionDate}) => {
        const npmView = generateTestNpmView(
                latestVersion,
                betaVersion,
                latestVersionDate,
                betaVersionDate,
            );
            const npmUtils = await import("../../common/npmUtils.js");
            const spy = vi
                .spyOn(npmUtils, "tryGetNpmView")
                .mockImplementation(async () => npmView);
        const npmVersion = await getApiVersionTypeFromNpm("test");
        expect(npmVersion).toBe(expectedVersionType);
        spy.mockRestore();
    });
})
