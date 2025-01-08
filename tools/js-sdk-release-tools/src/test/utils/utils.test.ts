import { describe, expect, it } from "vitest";
import { isManagementPlaneModularClient } from "../../utils/generateInputUtils";
import path, { resolve } from "path";
import { getRandomInt } from "./utils";
import { ensureDir, exists, remove, writeFile } from "fs-extra";

interface TestCase {
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
    ])("should be $expected for when typespec folder is $typespecFolder and tspconfig's isModularLibrary is $isModularLibrary", async (c: TestCase) => {
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
