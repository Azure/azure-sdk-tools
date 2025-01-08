import path from 'path';
import { describe, expect, it, test } from 'vitest';
import { getRandomInt } from '../utils/utils';
import { ensureDir, exists, remove, writeFile } from 'fs-extra';
import { stringify } from 'yaml';
import { SDKType } from '../../common/types';
import { isManagementPlaneModularClient, parseInputJson } from '../../utils/generateInputUtils';

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
                '@azure-tools/typespec-ts': {
                    flavor: 'azure'
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


interface ModularTypeTestCase {
    typespecFolder: undefined | string | string[];
    isModularLibrary: boolean | undefined;
    expected: boolean;
}

describe("Modular type", () => {
    it.each([
        // no typespec folder (autorest)
        { typespecFolder: undefined, isModularLibrary: true, expected: false },
        { typespecFolder: undefined, isModularLibrary:undefined, expected: false },
        
        // tspconfig has explicit isModularLibrary
        { typespecFolder: "A", isModularLibrary: true, expected: true },
        { typespecFolder: "A", isModularLibrary: false, expected: false },
        { typespecFolder: "A.Management", isModularLibrary: true, expected: true },
        { typespecFolder: "A.Management", isModularLibrary: false, expected: false },

        // tspconfig has no explicit isModularLibrary
        { typespecFolder: "A", isModularLibrary:undefined , expected: false},
        { typespecFolder: "A.Management", isModularLibrary:undefined , expected: true},
        { typespecFolder: "A.Management/", isModularLibrary:undefined , expected: true},
        { typespecFolder: ["A.Management/"], isModularLibrary:undefined , expected: true},
    ])("should be $expected for when typespec folder is $typespecFolder and tspconfig's isModularLibrary is $isModularLibrary", async (c: ModularTypeTestCase) => {
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}/azure-rest-api-specs`
        );
        if (c.typespecFolder !== undefined) {
            const typespecFolder = Array.isArray(c.typespecFolder) ? c.typespecFolder[0] : c.typespecFolder;
            const resolvedTypespecFolder = path.join(tempSpecFolder, typespecFolder);
            await ensureDir(resolvedTypespecFolder);
            const tspconfigLite = `
            options:
              "@azure-tools/typespec-ts":
                flavor: azure
                ${c.isModularLibrary !== undefined? 'isModularLibrary: ' + c.isModularLibrary : ''}
            `;
            const tspConfigPath = path.join(resolvedTypespecFolder, "tspconfig.yaml");
            await writeFile(tspConfigPath, tspconfigLite, { encoding: "utf8", flush: true });
        }

        try {
            const isModularLibrary = await isManagementPlaneModularClient(
                tempSpecFolder,
                c.typespecFolder
            );
            expect(isModularLibrary).toBe(c.expected);
        } finally {
            if (await exists(tempSpecFolder)) {
                await remove(tempSpecFolder);
            }
        }
    });
});