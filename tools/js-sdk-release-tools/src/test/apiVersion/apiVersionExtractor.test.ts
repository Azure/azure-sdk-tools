import { describe, expect, test, vi } from 'vitest';
import { getApiVersionType } from '../../mlc/apiVersion/apiVersionTypeExtractor.js';
import { getApiVersionType as getApiVersionTypeInRLC } from '../../llc/apiVersion/apiVersionTypeExtractor.js';
import { join } from 'path';
import { ApiVersionType } from '../../common/types.js';
import { tryFindApiVersionInRestClient } from '../../xlc/apiVersion/utils.js';

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

            const root = join(__dirname, "testCases/mlc-model-only/");
            const version1 = await getApiVersionType(root);
            expect(version1).toBe(ApiVersionType.Preview);
            const version2 = await getApiVersionType(root);
            expect(version2).toBe(ApiVersionType.Stable);
            spy.mockRestore();
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
