import path from 'path';
import { describe, expect, it, test } from 'vitest';
import { getRandomInt } from '../utils/utils';
import { emptyDir, remove, writeFile } from 'fs-extra';
import { stringify } from 'yaml';
import { SDKType } from '../../common/types';
import { getSDKType, parseInputJson } from '../../utils/generateInputUtils';

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
            await emptyDir(path.join(tempSpecFolder, 'tsp'));

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
            await emptyDir(path.join(tempSpecFolder, 'tsp'));
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
    typespecFolder?: string;
    ReadMe?: string;
    isModularLibrary?: boolean;
    expected: SDKType;
}

describe("SDK type", () => {
    it.each([
        // no typespec folder (autorest)
        {
            typespecFolder: undefined,
            ReadMe: "resource-manager/aaa.md",
            expected: SDKType.HighLevelClient,
        },
        {
            typespecFolder: undefined,
            ReadMe: "data-plane/aaa.md",
            expected: SDKType.RestLevelClient,
        },

        // tspconfig has explicit isModularLibrary
        {
            typespecFolder: "A",
            isModularLibrary: true,
            expected: SDKType.ModularClient,
        },
        {
            typespecFolder: "A",
            isModularLibrary: false,
            expected: SDKType.RestLevelClient,
        },
        {
            typespecFolder: "A.Management",
            isModularLibrary: true,
            expected: SDKType.ModularClient,
        },
        {
            typespecFolder: "A.Management",
            isModularLibrary: false,
            expected: SDKType.RestLevelClient,
        },

        // tspconfig has no explicit isModularLibrary
        {
            typespecFolder: "A",
            isModularLibrary: undefined,
            expected: SDKType.RestLevelClient,
        },
        {
            typespecFolder: "A.Management",
            isModularLibrary: undefined,
            expected: SDKType.ModularClient,
        },
        {
            typespecFolder: "A.Management/",
            isModularLibrary: undefined,
            expected: SDKType.ModularClient,
        },
    ])(
        "should be $expected for when readme is $ReadMe, typespec folder is $typespecFolder and tspconfig's isModularLibrary is $isModularLibrary",
        async (c: ModularTypeTestCase) => {
            const tempSpecFolder = path.join(
                __dirname,
                `tmp/spec-${getRandomInt(10000)}/azure-rest-api-specs`
            );
            if (c.typespecFolder !== undefined) {
                const typespecFolder = Array.isArray(c.typespecFolder)
                    ? c.typespecFolder[0]
                    : c.typespecFolder;
                const resolvedTypespecFolder = path.join(
                    tempSpecFolder,
                    typespecFolder
                );
                await emptyDir(resolvedTypespecFolder);
                const tspconfigLite = `
            options:
              "@azure-tools/typespec-ts":
                flavor: azure
                ${
                    c.isModularLibrary !== undefined
                        ? "isModularLibrary: " + c.isModularLibrary
                        : ""
                }
            `;
                const tspConfigPath = path.join(
                    resolvedTypespecFolder,
                    "tspconfig.yaml"
                );
                await writeFile(tspConfigPath, tspconfigLite, {
                    encoding: "utf8",
                    flush: true,
                });
            }

            try {
                const isModularLibrary = await getSDKType(
                    tempSpecFolder,
                    c.ReadMe,
                    c.typespecFolder
                );
                expect(isModularLibrary).toBe(c.expected);
            } finally {
                await remove(tempSpecFolder);
            }
        }
    );
});