import { describe, expect, test } from 'vitest';
import { getApiVersionType } from '../../mlc/apiVersion/apiVersionTypeExtractor';
import { getApiVersionType as getApiVersionTypeInRLC } from '../../llc/apiVersion/apiVersionTypeExtractor';
import { join } from 'path';
import { ApiVersionType } from '../../common/types';
import { tryFindApiVersionInRestClient } from '../../xlc/apiVersion/utils';

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
    });
});
