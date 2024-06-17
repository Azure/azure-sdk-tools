import { expect, test } from "vitest";
import { extractExportAndGenerateChangelog } from "../changelog/extractMetaData";
import path from "path";
import { SDKType } from "../common/types";

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
        "Removed operation DataProductsCatalogs.listByResourceGroup_NEW"
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
        "Removed operation DataProductsCatalogs.get_NEW"
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
        "Removed operation DataProductsCatalogsOperations.listByResourceGroup_NEW"
    );
});
