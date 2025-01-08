import { expect, test } from "vitest";
import { extractExportAndGenerateChangelog } from "../../changelog/extractMetaData";
import path from "path";
import { SDKType } from "../../common/types";
import { describe } from "node:test";
import { tryReadNpmPackageChangelog } from "../../common/utils";
import { removeSync, outputFileSync } from "fs-extra";

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

        expect(changelog.addedOperation[0].line).toBe(
            "Added operation DataProductsCatalogsOperations.listByResourceGroup_NEW"
        );
        expect(changelog.removedOperation[0].line).toBe(
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

        expect(changelog.addedOperation[0].line).toBe(
            "Added operation DataProductsCatalogs.get_NEW"
        );
        expect(changelog.removedOperation[0].line).toBe(
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

        expect(changelog.addedOperation[0].line).toBe(
            "Added operation DataProductsCatalogsOperations.listByResourceGroup_NEW"
        );
        expect(changelog.removedOperation[0].line).toBe(
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

        expect(changelog.addedOperationGroup[0].line).toBe(
            "Added operation group DataProductsCatalogs_add"
        );
        expect(changelog.removedOperationGroup[0].line).toBe(
            "Removed operation group DataProductsCatalogs_remove"
        );
        expect(changelog.operationSignatureChange[0].line).toBe(
            "Operation DataProductsCatalogs_sig_change.get has a new signature"
        );
    });

    test("Patch RLC -> RLC's basic breaking changes", async () => {
        const root = path.join(
            __dirname,
            "../../../packages/typescript-codegen-breaking-change-detector/misc/test-cases/patch-detection/"
        );
        const oldViewPath = path.join(__dirname, `testCases/patch.1.old.rlc.api.md`);
        const newViewPath = path.join(__dirname, `testCases/patch.1.new.rlc.api.md`);
        const changelog = await extractExportAndGenerateChangelog(
            oldViewPath,
            newViewPath,
            SDKType.RestLevelClient,
            SDKType.RestLevelClient
        );
        console.log("changelog --");
        expect(changelog.displayChangeLog()).toBe(
            `### Features Added

  - Added operation in Routes for path: "add"
  - Added Interface ClusterGetOld
  - Added Type Alias typesAdd
  - Added function funcAdd
  - Added function overload "export function isUnexpected(response: C | E): response is C;"

### Breaking Changes

  - Removed operation Routes for path: "remove"
  - Operation in "Routes" has a new signature for path: "change_return_type"
  - Operation in "Routes" has a new signature for path: "change_para_count"
  - Operation in "Routes" has a new signature for path: "change_para_type"
  - Type of parameter a of interface ClustersGet is changed from number to string
  - Removed function funcRemove
  - Function funcReturnType has a new signature
  - Function funcParameterCount has a new signature
  - Function funcParameterType has a new signature
  - Removed function overload "export function isUnexpected(response: C | D): response is A;"
  - Removed Type Alias typesRemove
  - Type alias "typesChange" has been changed`
        );
    });
});

describe("Changelog reading", () => {
        test("Read changelog that doesn't exist", () => {
            const content = tryReadNpmPackageChangelog('./do/not/exist/CHANGELOG.md');
            expect(content).toBe("");
        });
    
        test("Read changelog that exists", () => {
            const changelogPath = path.join('CHANGELOG.md')
            try {
                outputFileSync(changelogPath, 'aaa', 'utf-8');
                const content = tryReadNpmPackageChangelog(changelogPath);
                expect(content).toBe("aaa");
            } finally {
                removeSync(changelogPath);
            }
        })
});
