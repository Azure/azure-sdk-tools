import path from 'path';
import { describe, expect, test } from 'vitest';
import { getRandomInt } from '../utils/utils';
import { ensureDir, remove, writeFile } from 'fs-extra';
import { stringify } from 'yaml';
import { SDKType } from '../../common/types';
import { parseInputJson } from '../../utils/generateInputUtils';

describe('Auto SDK generation path test', () => {
    test('high level client generation', async () => {
        const inputJson = {
            relatedReadmeMdFiles: ['zzz/resource-manager/xxx.readme']
        };
        const { sdkType } = await parseInputJson(inputJson);
        expect(sdkType).toBe(SDKType.HighLevelClient);
    });

    test('rest level client generation', async () => {
        const fakeTspConfig = {
            options: {
                '@azure-tools/typespec-ts': {}
            }
        };
        const tempSpecFolder = path.join(__dirname, `tmp/spec-${getRandomInt(10000)}`);
        const inputJson = {
            relatedTypeSpecProjectFolder: ['tsp'],
            specFolder: tempSpecFolder
        };
        try {
            await ensureDir(path.join(tempSpecFolder, 'tsp'));

            await writeFile(path.join(tempSpecFolder, 'tsp/tspconfig.yaml'), stringify(fakeTspConfig), {
                encoding: 'utf8',
                flush: true
            });
            const { sdkType } = await parseInputJson(inputJson);
            expect(sdkType).toBe(SDKType.RestLevelClient);
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test('modular client generation', async () => {
        const fakeTspConfig = {
            options: {
                '@azure-tools/typespec-ts': {
                    isModularLibrary: true
                }
            }
        };
        const tempSpecFolder = path.join(__dirname, `tmp/spec-${getRandomInt(10000)}`);
        const inputJson = {
            relatedTypeSpecProjectFolder: ['tsp'],
            specFolder: tempSpecFolder
        };
        try {
            await ensureDir(path.join(tempSpecFolder, 'tsp'));
            await writeFile(path.join(tempSpecFolder, 'tsp/tspconfig.yaml'), stringify(fakeTspConfig), {
                encoding: 'utf8',
                flush: true
            });
            const { sdkType } = await parseInputJson(inputJson);
            expect(sdkType).toBe(SDKType.ModularClient);
        } finally {
            await remove(tempSpecFolder);
        }
    });
});
