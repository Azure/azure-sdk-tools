import { expect, test } from 'vitest'
import { extractExportAndGenerateChangelog } from '../src/changelog/extractMetaData'
import path from 'path';

test('HLC -> Modular: Rename', async () => {
  const oldViewPath = path.join(__dirname, "testCases/operationGroups.old.api.md");
  const newViewPath = path.join(__dirname, "testCases/operationGroups.new.api.md");
  const changelog = await extractExportAndGenerateChangelog(oldViewPath, newViewPath);
  
  expect(changelog.addedOperationGroup.length).toBe(0);
  expect(changelog.removedOperationGroup.length).toBe(0);
  expect(changelog.interfaceParamDelete.length).toBe(1);
  expect(changelog.interfaceParamDelete[0]).toBe('Interface DataProductsCatalogsOperations no longer has parameter listByResourceXXXGroup');
  expect(changelog.interfaceParamAddRequired.length).toBe(1);
  expect(changelog.interfaceParamAddRequired[0]).toBe('Interface DataProductsCatalogsOperations has a new required parameter listByResourceGroup');
})