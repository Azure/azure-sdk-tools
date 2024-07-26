import { expect, test } from "vitest";
import { extractExportAndGenerateChangelog } from "../../changelog/extractMetaData";
import path, { join } from "path";
import { SDKType } from "../../common/types";
import { describe } from "node:test";
import { tryReadNpmPackageChangelog } from "../../common/utils";
import { writeFileSync } from "fs";
import ts from "typescript";
import { mkdir, rm, rmdir, writeFile } from "fs/promises";

function getRandomInt(max) {
    return Math.floor(Math.random() * max);
}

describe("Breaking change detection", () => {
    test("HLC -> Modular: Rename", async () => {
        const oldViewPath = path.join(
            __dirname,
            "testCases/operationGroups.1.old.hlc.api.md"
        );
        const newViewPath = path.join(
            __dirname,
            "testCases/operationGroups.1.new.modular.api.md"
        );
        const changelog = await extractExportAndGenerateChangelog(
            oldViewPath,
            newViewPath,
            SDKType.HighLevelClient,
            SDKType.ModularClient
        );

        expect(changelog.addedOperationGroup.length).toBe(0);
        expect(changelog.removedOperationGroup.length).toBe(0);

        expect(changelog.addedOperation.length).toBe(1);
        expect(changelog.removedOperation.length).toBe(1);

        expect(changelog.addedOperation[0]).toBe(
            "Added operation DataProductsCatalogsOperations.listByResourceGroup_NEW"
        );
        expect(changelog.removedOperation[0]).toBe(
            "Removed operation DataProductsCatalogs.listByResourceGroup"
        );
    });

    test("HLC -> HLC: Change Op", async () => {
        const oldViewPath = path.join(
            __dirname,
            "testCases/operationGroups.2.old.hlc.api.md"
        );
        const newViewPath = path.join(
            __dirname,
            "testCases/operationGroups.2.new.hlc.api.md"
        );
        const changelog = await extractExportAndGenerateChangelog(
            oldViewPath,
            newViewPath,
            SDKType.HighLevelClient,
            SDKType.HighLevelClient
        );

        expect(changelog.addedOperationGroup.length).toBe(0);
        expect(changelog.removedOperationGroup.length).toBe(0);

        expect(changelog.addedOperation.length).toBe(1);
        expect(changelog.removedOperation.length).toBe(1);

        expect(changelog.addedOperation[0]).toBe(
            "Added operation DataProductsCatalogs.get_NEW"
        );
        expect(changelog.removedOperation[0]).toBe(
            "Removed operation DataProductsCatalogs.get"
        );
    });

    test("Modular -> Modular: Change Op", async () => {
        const oldViewPath = path.join(
            __dirname,
            "testCases/operationGroups.3.old.modular.api.md"
        );
        const newViewPath = path.join(
            __dirname,
            "testCases/operationGroups.3.new.modular.api.md"
        );
        const changelog = await extractExportAndGenerateChangelog(
            oldViewPath,
            newViewPath,
            SDKType.ModularClient,
            SDKType.ModularClient
        );

        expect(changelog.addedOperationGroup.length).toBe(0);
        expect(changelog.removedOperationGroup.length).toBe(0);

        expect(changelog.addedOperation.length).toBe(1);
        expect(changelog.removedOperation.length).toBe(1);

        expect(changelog.addedOperation[0]).toBe(
            "Added operation DataProductsCatalogsOperations.listByResourceGroup_NEW"
        );
        expect(changelog.removedOperation[0]).toBe(
            "Removed operation DataProductsCatalogsOperations.listByResourceGroup"
        );
    });

    test("HLC -> HLC: Operation Group Add/Remove/ChangeSig", async () => {
        const oldViewPath = path.join(
            __dirname,
            "testCases/operationGroups.4.old.hlc.api.md"
        );
        const newViewPath = path.join(
            __dirname,
            "testCases/operationGroups.4.new.hlc.api.md"
        );
        const changelog = await extractExportAndGenerateChangelog(
            oldViewPath,
            newViewPath,
            SDKType.HighLevelClient,
            SDKType.HighLevelClient
        );

        expect(changelog.addedOperationGroup.length).toBe(1);
        expect(changelog.removedOperationGroup.length).toBe(1);

        expect(changelog.addedOperation.length).toBe(0);
        expect(changelog.removedOperation.length).toBe(0);

        expect(changelog.operationSignatureChange.length).toBe(1);

        expect(changelog.addedOperationGroup[0]).toBe(
            "Added operation group DataProductsCatalogs_add"
        );
        expect(changelog.removedOperationGroup[0]).toBe(
            "Removed operation group DataProductsCatalogs_remove"
        );
        expect(changelog.operationSignatureChange[0]).toBe(
            "Operation DataProductsCatalogs_sig_change.get has a new signature"
        );
    });
});

describe("Changelog reading", () => {
    const tempPackageFolder = "./temp/not/exist";
    test("Read changelog that doesn't exist", () => {
        const content = tryReadNpmPackageChangelog(tempPackageFolder);
        expect(content).toBe("");
    });
    const changelogPath = join(tempPackageFolder, "CHANGELOG.md");
    writeFileSync(changelogPath, "aaa", "utf-8");

    test("Read changelog that exists", () => {
        const content = tryReadNpmPackageChangelog(tempPackageFolder);
        expect(content).toBe("aaa");
    });
});

describe("Fix breaking change detection", function () {
    // The issue:
    //   export type ProvisioningState = "succeeded" | "provisioning" | "failed" ->
    //   export type ProvisioningState = "Succeeded" | "Provisioning" | "Failed"
    const tempPackageFolder = join(
        __dirname,
        `./.tmp-package-${getRandomInt(10000)}`
    );
    const oldPath = join(tempPackageFolder, "old.ts");
    const newPath = join(tempPackageFolder, "new.ts");
    test("Improve union enum breaking change detection", async function () {
        try {
            const oldCode =
                'export type ProvisioningState = "succeeded" | "provisioning" | "failed"';
            const newCode =
                'export type ProvisioningState = "Succeeded" | "Provisioning" | "Failed"';
            await mkdir(tempPackageFolder);
            await writeFile(oldPath, oldCode);
            await writeFile(newPath, newCode);
            const compilerOptions = {
                module: ts.ModuleKind.CommonJS,
                target: ts.ScriptTarget.ES2022,
            };
            const program = ts.createProgram(
                [oldPath, newPath],
                compilerOptions
            );
            const checker = program.getTypeChecker();

            const oldFile = program.getSourceFile(oldPath);
            const newFile = program.getSourceFile(newPath);
            const getType = (file: ts.SourceFile) => {
                const node = (file.statements[0] as ts.TypeAliasDeclaration)
                    .type;
                return checker.getTypeAtLocation(node);
            };
            const oldType = getType(oldFile!);
            const newType = getType(newFile!);
            const result = checker.isTypeAssignableTo(oldType, newType);
            expect(result).toBe(false);
        } finally {
            await rm(oldPath);
            await rm(newPath);
            await rmdir(tempPackageFolder);
        }
    });
});
